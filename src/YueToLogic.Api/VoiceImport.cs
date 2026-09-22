using System.Security.Cryptography;
using YueToLogic.Core.Diagnostics;
using YueToLogic.Core.Logic;
using YueToLogic.Core.Voices;

namespace YueToLogic.Api;

/// <summary>
/// Fetches the converted vocals of a finished voice job so that they can go straight onto the project's
/// vocals track. The browser only passes the job id; the file travels from ChangeMyVoice to this server and
/// into the package, without the detour of a download and an upload.
/// </summary>
/// <remarks>
/// The Logic template's audio tracks are prepared for 48 kHz, which is what YuE writes and what StemMyWav
/// hands back. ChangeMyVoice works at its model's own rate, so what comes back is checked here rather than in
/// the writer: a recording Logic cannot take leaves the project with the separated vocals and a warning,
/// instead of failing an export that is otherwise fine. The service names the result's checksum, which is
/// compared as well - a transfer that broke off would otherwise end up in the project as a truncated file.
/// </remarks>
public sealed class VoiceImport : IAsyncDisposable
{
    private FileStream? file;

    /// <summary>The converted recording, or null when there is none Logic could play.</summary>
    public Stream? Vocals => file;

    /// <summary>What went wrong, if it could not be used; the export then carries on without it.</summary>
    public Diagnostic? Problem { get; private set; }

    public bool HasVocals => file is not null;

    public static async Task<VoiceImport> FetchAsync(
        IVoiceConversionService? service,
        string job,
        CancellationToken cancellationToken)
    {
        var import = new VoiceImport();
        if (service is null)
        {
            import.Problem = Warning("This server has no voice service, so the project keeps the separated vocals.");
            return import;
        }

        try
        {
            // The status is asked for first, because it carries the checksum the transfer is measured against.
            var state = await service.GetJobAsync(job, cancellationToken).ConfigureAwait(false);

            var converted = TemporaryFile();
            import.file = converted;
            await using (var result = await service.DownloadResultAsync(job, cancellationToken).ConfigureAwait(false))
            {
                await result.CopyToAsync(converted, cancellationToken).ConfigureAwait(false);
            }

            converted.Position = 0;
            await import.CheckAsync(state.ResultSha256, cancellationToken).ConfigureAwait(false);
        }
        catch (VoiceConversionException exception)
        {
            await import.DropAsync().ConfigureAwait(false);
            import.Problem = Warning($"The converted vocals could not be fetched, so the project keeps the separated ones: {exception.Message}");
        }

        return import;
    }

    public async ValueTask DisposeAsync() => await DropAsync().ConfigureAwait(false);

    /// <summary>
    /// Checks that the file arrived whole and that Logic can take it at all. The stream is rewound after
    /// every read, so the writer still gets it from the first byte.
    /// </summary>
    private async Task CheckAsync(string? expectedSha256, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false));
            file.Position = 0;
            if (!hash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                await DropAsync().ConfigureAwait(false);
                Problem = Warning("The converted vocals did not arrive whole - their checksum differs from the one the voice service named; the project keeps the separated vocals.");
                return;
            }
        }

        var header = new byte[AudioStreamInfo.HeaderLength];
        var length = await file.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false);
        file.Position = 0;

        if (!AudioStreamInfo.TryParse(header.AsSpan(0, length), out var info))
        {
            await DropAsync().ConfigureAwait(false);
            Problem = Warning("The voice service sent something that is neither a WAV nor a FLAC; the project keeps the separated vocals.");
        }
        else if (info!.SampleRate != LogicProjectWriter.RequiredSampleRate)
        {
            await DropAsync().ConfigureAwait(false);
            Problem = Warning(
                $"The converted vocals have {info.SampleRate} Hz, the Logic project needs {LogicProjectWriter.RequiredSampleRate} Hz. " +
                "The project keeps the separated vocals; the converted ones can be downloaded and brought in by hand.");
        }
    }

    private async Task DropAsync()
    {
        if (file is not null)
        {
            await file.DisposeAsync().ConfigureAwait(false);
            file = null;
        }
    }

    /// <summary>A file that is gone as soon as it is closed, and on Unix already unlinked while it is open.</summary>
    private static FileStream TemporaryFile() =>
        new(
            Path.Combine(Path.GetTempPath(), $"yue-to-logic-voice-{Guid.NewGuid():N}.tmp"),
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);

    private static Diagnostic Warning(string message) =>
        new(DiagnosticSeverity.Warning, DiagnosticCodes.VoiceUnavailable, message, null, null);
}
