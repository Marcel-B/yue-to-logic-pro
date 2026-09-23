using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using YueToLogic.Core.Conversion;
using YueToLogic.Core.Serialization;

namespace YueToLogic.Api;

/// <summary>
/// The way back: a MIDI file, typically exported from Logic after editing the project there, into a YuE2
/// <c>score.abc</c>. Takes a multipart form with the fields <c>file</c> (the MIDI file) and optionally
/// <c>options</c> (<see cref="MidiToAbcOptions"/> as JSON).
/// </summary>
public static class MidiEndpoints
{
    /// <summary>A song's MIDI file is a few hundred kilobytes at most, even with every generated track in it.</summary>
    public const long MaxMidiBytes = 4 * 1024 * 1024;

    public static RouteGroupBuilder MapMidiEndpoints(this RouteGroupBuilder api)
    {
        // The tracks come back with the score, so a client can show which one became which voice and change it.
        api.MapPost("/midi/abc", ConvertToAbcAsync)
            .DisableAntiforgery()
            .WithName("MidiToAbc")
            .WithSummary("Converts a MIDI file into a YuE2 score.abc and returns it with the score, the file's tracks and their roles, and diagnostics as JSON.");

        return api;
    }

    private static async Task<IResult> ConvertToAbcAsync(
        IFormFile? file,
        [FromForm] string? options,
        IMidiToAbcConverter converter,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("Missing MIDI file", "Send the MIDI file as form field 'file'.");
        }

        if (file.Length > MaxMidiBytes)
        {
            return BadRequest("MIDI file too large", $"A MIDI file may have at most {MaxMidiBytes / (1024 * 1024)} MB.");
        }

        MidiToAbcOptions parsed;
        try
        {
            parsed = string.IsNullOrWhiteSpace(options)
                ? new MidiToAbcOptions()
                : JsonSerializer.Deserialize(options, YueToLogicJsonContext.Default.MidiToAbcOptions) ?? new MidiToAbcOptions();
        }
        catch (JsonException ex)
        {
            return BadRequest("Invalid options", $"Form field 'options' is not valid JSON: {ex.Message}");
        }

        await using var stream = file.OpenReadStream();
        var result = await converter.ConvertAsync(stream, parsed, cancellationToken);
        return Results.Json(
            result,
            YueToLogicJsonContext.Default.MidiToAbcResult,
            statusCode: result.Success ? StatusCodes.Status200OK : StatusCodes.Status422UnprocessableEntity);
    }

    private static IResult BadRequest(string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: StatusCodes.Status400BadRequest);
}
