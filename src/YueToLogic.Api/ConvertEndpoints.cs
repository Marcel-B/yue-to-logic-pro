using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using YueToLogic.Core.Conversion;
using YueToLogic.Core.Diagnostics;
using YueToLogic.Core.Logic;
using YueToLogic.Core.Serialization;
using YueToLogic.Core.Stems;

namespace YueToLogic.Api;

/// <summary>
/// HTTP endpoints for converting a YuE2 <c>score.abc</c>. Both take a multipart form with the fields
/// <c>file</c> (the score) and optionally <c>options</c> (<see cref="ConversionOptions"/> as JSON).
/// </summary>
public static class ConvertEndpoints
{
    /// <summary>YuE2 scores are a few kilobytes; anything much larger is not a score.</summary>
    public const long MaxScoreBytes = 1024 * 1024;

    /// <summary>YuE writes about 10 MB of FLAC per minute; this leaves room for songs of 20 minutes and more.</summary>
    public const long MaxAudioBytes = 250L * 1024 * 1024;

    /// <summary>Response header with the warnings of a Logic export, as a JSON array of diagnostics.</summary>
    public const string DiagnosticsHeader = "X-YueToLogic-Diagnostics";

    // Header values must be single-line, so the diagnostics header uses unindented JSON.
    private static readonly YueToLogicJsonContext CompactJson = new(new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    });

    public static RouteGroupBuilder MapConvertEndpoints(this RouteGroupBuilder api)
    {
        // Returns the parsed score, all diagnostics and the MIDI file (base64) in one JSON document.
        api.MapPost("/convert", ConvertAsync)
            .DisableAntiforgery()
            .WithName("Convert")
            .WithSummary("Converts a score.abc and returns score, diagnostics and MIDI (base64) as JSON.");

        // Returns only the MIDI file, for clients that do not need the score (e.g. curl).
        api.MapPost("/convert/midi", ConvertToMidiAsync)
            .DisableAntiforgery()
            .WithName("ConvertToMidi")
            .WithSummary("Converts a score.abc and returns the Standard MIDI File.");

        // Returns a ZIP with a Logic Pro project (.logicx) built from the score and, if one was uploaded, the audio.flac.
        api.MapPost("/convert/logic", ConvertToLogicAsync)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxAudioBytes + MaxScoreBytes + 64 * 1024))
            .WithFormOptions(multipartBodyLengthLimit: MaxAudioBytes + MaxScoreBytes)
            .WithName("ConvertToLogic")
            .WithSummary("Converts a score.abc, optionally with its audio.flac, into a zipped Logic Pro project. The form field 'splitSections' gives every track one region per song section, 'stemJob' puts the stems of a finished separation on their own audio tracks.");

        return api;
    }

    private static async Task<IResult> ConvertToLogicAsync(
        IFormFile? file,
        IFormFile? audio,
        [FromForm] string? options,
        [FromForm] string? name,
        [FromForm] bool? splitSections,
        [FromForm] Guid? stemJob,
        IScoreConverter converter,
        ILogicProjectWriter writer,
        IServiceProvider services,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (audio is { Length: > MaxAudioBytes })
        {
            return BadRequest("Audio file too large", $"The audio may have at most {MaxAudioBytes / (1024 * 1024)} MB.");
        }

        var (result, problem, parsed) = await RunAsync(file, options, converter, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        if (!result!.Success)
        {
            return Results.Json(result, YueToLogicJsonContext.Default.ConversionResult, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var packageName = PackageName(name, file!.FileName);

        // ZipArchive writes synchronously, which Kestrel does not allow on the response; build the ZIP in a
        // temporary file (deleted when the response has been sent) instead of holding the audio in memory.
        var zip = new FileStream(
            Path.Combine(Path.GetTempPath(), $"yue-to-logic-{Guid.NewGuid():N}.zip"),
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);

        // The separated stems come from the stem service rather than from the browser, which only knows the job.
        var stemService = services.GetService<IStemSeparationService>();
        await using var stems = stemJob is { } job
            ? await StemImport.FetchAsync(stemService, job, cancellationToken)
            : null;

        LogicProjectResult logic;
        try
        {
            await using var audioStream = audio is { Length: > 0 } ? audio.OpenReadStream() : null;
            using (var sink = new ZipLogicPackageSink(zip, packageName))
            {
                var logicOptions = new LogicProjectOptions
                {
                    ProjectName = packageName,
                    SplitRegionsAtSections = splitSections ?? false,
                    Channels = parsed.MidiChannels,
                };
                var logicAudio = new LogicAudio(audioStream, stems?.Vocals, stems?.VocalsDry);
                logic = await writer.WriteAsync(result.Score!, logicAudio, sink, logicOptions, cancellationToken);
            }
        }
        catch
        {
            await zip.DisposeAsync();
            throw;
        }

        // The stems are in the package now, so the service may drop them; an unconfirmed job it clears itself.
        if (logic.Success && stemJob is { } imported && stems?.HasStems == true && stemService is not null)
        {
            await stemService.DeleteAsync(imported, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<Diagnostic> diagnostics =
            [.. result.Diagnostics, .. logic.Diagnostics, .. stems?.Problem is { } stemProblem ? new[] { stemProblem } : []];
        if (!logic.Success)
        {
            await zip.DisposeAsync();
            return Results.Json(
                new ConversionResult(false, null, null, diagnostics),
                YueToLogicJsonContext.Default.ConversionResult,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var warnings = diagnostics.Where(d => d.Severity != DiagnosticSeverity.Info).ToArray();
        if (warnings.Length > 0)
        {
            // JSON escapes non-ASCII characters by default, so the value is a valid header.
            context.Response.Headers[DiagnosticsHeader] = JsonSerializer.Serialize(warnings, CompactJson.DiagnosticArray);
        }

        zip.Position = 0;
        return Results.File(zip, "application/zip", $"{packageName}.logicx.zip");
    }

    /// <summary>A safe file name from the requested name, else from the score's file name.</summary>
    private static string PackageName(string? requested, string scoreFileName)
    {
        var candidate = string.IsNullOrWhiteSpace(requested) ? Path.GetFileNameWithoutExtension(scoreFileName) : requested;
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':']).ToHashSet();
        var cleaned = new string(candidate.Where(c => !invalid.Contains(c) && !char.IsControl(c)).ToArray()).Trim().TrimStart('.');
        if (cleaned.Length > 100)
        {
            cleaned = cleaned[..100];
        }

        return cleaned.Length == 0 ? "YuE" : cleaned;
    }

    private static async Task<IResult> ConvertAsync(
        IFormFile? file,
        [FromForm] string? options,
        IScoreConverter converter,
        CancellationToken cancellationToken)
    {
        var (result, problem, _) = await RunAsync(file, options, converter, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        return Results.Json(
            result,
            YueToLogicJsonContext.Default.ConversionResult,
            statusCode: result!.Success ? StatusCodes.Status200OK : StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> ConvertToMidiAsync(
        IFormFile? file,
        [FromForm] string? options,
        IScoreConverter converter,
        CancellationToken cancellationToken)
    {
        var (result, problem, _) = await RunAsync(file, options, converter, cancellationToken);
        if (problem is not null)
        {
            return problem;
        }

        if (!result!.Success)
        {
            return Results.Json(result, YueToLogicJsonContext.Default.ConversionResult, statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var downloadName = Path.ChangeExtension(Path.GetFileName(file!.FileName), ".mid");
        return Results.File(result.Midi!, "audio/midi", string.IsNullOrWhiteSpace(downloadName) ? "score.mid" : downloadName);
    }

    private static async Task<(ConversionResult? Result, IResult? Problem, ConversionOptions Options)> RunAsync(
        IFormFile? file,
        string? optionsJson,
        IScoreConverter converter,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return (null, BadRequest("Missing score file", "Send the score.abc as form field 'file'."), new ConversionOptions());
        }

        if (file.Length > MaxScoreBytes)
        {
            return (null, BadRequest("Score file too large", $"A score.abc is at most {MaxScoreBytes / 1024} KB."), new ConversionOptions());
        }

        ConversionOptions options;
        try
        {
            options = string.IsNullOrWhiteSpace(optionsJson)
                ? new ConversionOptions()
                : JsonSerializer.Deserialize(optionsJson, YueToLogicJsonContext.Default.ConversionOptions) ?? new ConversionOptions();
        }
        catch (JsonException ex)
        {
            return (null, BadRequest("Invalid options", $"Form field 'options' is not valid JSON: {ex.Message}"), new ConversionOptions());
        }

        await using var stream = file.OpenReadStream();
        return (await converter.ConvertAsync(stream, options, cancellationToken), null, options);
    }

    private static IResult BadRequest(string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: StatusCodes.Status400BadRequest);
}
