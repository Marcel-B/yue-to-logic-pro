using System.IO.Compression;
using YueToLogic.Core.Diagnostics;
using YueToLogic.Core.Stems;

namespace YueToLogic.Api;

/// <summary>
/// Fetches the stems of a finished separation so that they can go straight into a Logic project. The browser
/// only passes the job id; the files travel from StemMyWav to this server and into the package, without the
/// detour of a download and an upload.
/// </summary>
public sealed class StemImport : IAsyncDisposable
{
    /// <summary>The files of the result ZIP, in the order the project's audio tracks want them.</summary>
    private static readonly string[] Wanted = ["vocals.wav", "vocals_dry.wav"];

    private readonly List<Stream> open = [];

    public Stream? Vocals { get; private set; }

    public Stream? VocalsDry { get; private set; }

    /// <summary>What went wrong, if the stems could not be fetched; the export then carries on without them.</summary>
    public Diagnostic? Problem { get; private set; }

    public bool HasStems => Vocals is not null || VocalsDry is not null;

    /// <summary>
    /// Downloads the result and opens the WAV files it holds. The ZIP goes to a temporary file, since reading
    /// an archive needs to seek, and that file is removed as soon as it is closed.
    /// </summary>
    public static async Task<StemImport> FetchAsync(
        IStemSeparationService? service,
        Guid job,
        CancellationToken cancellationToken)
    {
        var import = new StemImport();
        if (service is null)
        {
            import.Problem = Warning("This server has no stem service, so the project is written without stems.");
            return import;
        }

        try
        {
            var archive = TemporaryFile();
            import.open.Add(archive);
            await using (var result = await service.DownloadAsync(job, cancellationToken).ConfigureAwait(false))
            {
                await result.CopyToAsync(archive, cancellationToken).ConfigureAwait(false);
            }

            archive.Position = 0;
            import.Read(new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true));
        }
        catch (StemSeparationException exception)
        {
            import.Problem = Warning($"The stems could not be fetched, so the project is written without them: {exception.Message}");
        }
        catch (InvalidDataException)
        {
            import.Problem = Warning("The stem service sent something that is not a ZIP; the project is written without stems.");
        }

        return import;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var stream in open)
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Copies the wanted files out of the archive, each into a temporary file of its own.</summary>
    private void Read(ZipArchive archive)
    {
        using (archive)
        {
            foreach (var name in Wanted)
            {
                var entry = archive.Entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));
                if (entry is null)
                {
                    continue;
                }

                var file = TemporaryFile();
                open.Add(file);
                using (var source = entry.Open())
                {
                    source.CopyTo(file);
                }

                file.Position = 0;
                if (name == Wanted[0])
                {
                    Vocals = file;
                }
                else
                {
                    VocalsDry = file;
                }
            }
        }

        if (!HasStems)
        {
            Problem = Warning("The result of the separation holds no vocals; the project is written without stems.");
        }
    }

    /// <summary>A file that is gone as soon as it is closed, and on Unix already unlinked while it is open.</summary>
    private static FileStream TemporaryFile() =>
        new(
            Path.Combine(Path.GetTempPath(), $"yue-to-logic-stem-{Guid.NewGuid():N}.tmp"),
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);

    private static Diagnostic Warning(string message) =>
        new(DiagnosticSeverity.Warning, DiagnosticCodes.StemsUnavailable, message, null, null);
}
