using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using YueToLogic.Core.Conversion;
using YueToLogic.Core.Serialization;

namespace YueToLogic.Api;

/// <summary>
/// HTTP endpoints for converting a YuE2 <c>score.abc</c>. Both take a multipart form with the fields
/// <c>file</c> (the score) and optionally <c>options</c> (<see cref="ConversionOptions"/> as JSON).
/// </summary>
public static class ConvertEndpoints
{
    /// <summary>YuE2 scores are a few kilobytes; anything much larger is not a score.</summary>
    public const long MaxScoreBytes = 1024 * 1024;

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

        return api;
    }

    private static async Task<IResult> ConvertAsync(
        IFormFile? file,
        [FromForm] string? options,
        IScoreConverter converter,
        CancellationToken cancellationToken)
    {
        var (result, problem) = await RunAsync(file, options, converter, cancellationToken);
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
        var (result, problem) = await RunAsync(file, options, converter, cancellationToken);
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

    private static async Task<(ConversionResult? Result, IResult? Problem)> RunAsync(
        IFormFile? file,
        string? optionsJson,
        IScoreConverter converter,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return (null, BadRequest("Missing score file", "Send the score.abc as form field 'file'."));
        }

        if (file.Length > MaxScoreBytes)
        {
            return (null, BadRequest("Score file too large", $"A score.abc is at most {MaxScoreBytes / 1024} KB."));
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
            return (null, BadRequest("Invalid options", $"Form field 'options' is not valid JSON: {ex.Message}"));
        }

        await using var stream = file.OpenReadStream();
        return (await converter.ConvertAsync(stream, options, cancellationToken), null);
    }

    private static IResult BadRequest(string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: StatusCodes.Status400BadRequest);
}
