using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using YueToLogic.Api;
using YueToLogic.Core.Conversion;
using YueToLogic.Core.Diagnostics;
using YueToLogic.Core.Model;
using YueToLogic.Core.Serialization;

namespace YueToLogic.Api.Tests;

public class ConvertEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Fitting_the_tempo_and_a_count_in_reach_the_conversion()
    {
        // The sample is 8 bars at 88 BPM, which is 21.818 s.
        const string options = """
            {"arrangement":{"countIn":{"bars":2}},"fitTempo":{"audioSeconds":21.0}}
            """;

        var response = await _client.PostAsync("/api/convert", Form(SampleScore, options));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), YueToLogicJsonContext.Default.ConversionResult)!;
        Assert.Equal(91.4286, result.Score!.TempoBpm, 4);
        Assert.Equal(21.0, result.Score.MusicDurationSeconds, 3);
        Assert.Equal(2 * 4 * 480, result.Score.CountInTicks);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.TempoFitted);
    }

    private static readonly string SampleScore = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", "score.abc"));

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_endpoint_answers()
    {
        Assert.Equal("ok", await _client.GetStringAsync("/api/health"));
    }

    [Fact]
    public async Task Convert_returns_score_diagnostics_and_midi_in_one_document()
    {
        var response = await _client.PostAsync("/api/convert", Form(SampleScore, """{"arrangement":{"bass":{"pattern":"rootFifth"},"drums":{}}}"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadResultAsync(response);
        Assert.True(result.Success);
        Assert.Equal(["Vocal", "Ins", "Bass", "Drums"], result.Score!.Voices.Select(v => v.Id));
        Assert.Equal("MThd", Encoding.ASCII.GetString(result.Midi!, 0, 4));
    }

    [Fact]
    public async Task Drum_notes_in_the_options_put_the_split_kit_on_a_drum_machine()
    {
        const string options = """{"arrangement":{"drums":{"separateTracks":true,"notes":{"kick":36,"snare":37,"closedHiHat":44,"openHiHat":45,"crash":51,"clap":39}},"countIn":{"bars":1}}}""";

        var response = await _client.PostAsync("/api/convert", Form(SampleScore, options));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var score = (await ReadResultAsync(response)).Score!;
        // The count-in click lands on the first drum track, as the machine's clap.
        Assert.Equal([36, 39], score.Voices.Single(v => v.Id == "Kick").Notes.Select(n => n.NoteNumber).Distinct().Order());
        Assert.Equal([37], score.Voices.Single(v => v.Id == "Snare").Notes.Select(n => n.NoteNumber).Distinct());
        Assert.Equal([44], score.Voices.Single(v => v.Id == "HiHat").Notes.Select(n => n.NoteNumber).Distinct());
        Assert.Equal([51], score.Voices.Single(v => v.Id == "Crash").Notes.Select(n => n.NoteNumber).Distinct());
    }

    [Fact]
    public async Task Chord_pattern_adds_a_played_out_chord_track()
    {
        var response = await _client.PostAsync("/api/convert", Form(SampleScore, """{"arrangement":{"chords":{"pattern":"offbeat"}}}"""));

        var result = await ReadResultAsync(response);
        Assert.True(result.Success);
        var chords = result.Score!.Voices.Single(v => v.Id == "Chords");
        Assert.Equal(TrackKind.Chords, chords.Kind);
        Assert.Equal(8 * 4 * 3, chords.Notes.Count); // eight bars, four off-beat chords each, three notes per chord
        Assert.Equal(62, chords.Notes[0].Velocity); // the default velocity of 72, softened off the beat: it survived the partial options document
    }

    [Fact]
    public async Task Options_are_optional()
    {
        var response = await _client.PostAsync("/api/convert", Form(SampleScore, options: null));

        var result = await ReadResultAsync(response);
        Assert.True(result.Success);
        Assert.Equal(480, result.Score!.TicksPerQuarterNote);
    }

    [Fact]
    public async Task Midi_endpoint_returns_a_downloadable_midi_file()
    {
        var response = await _client.PostAsync("/api/convert/midi", Form(SampleScore, options: null, fileName: "my song.abc"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("audio/midi", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("my song.mid", response.Content.Headers.ContentDisposition!.FileNameStar);
        Assert.Equal("MThd"u8.ToArray(), (await response.Content.ReadAsByteArrayAsync())[..4]);
    }

    [Fact]
    public async Task Invalid_option_values_are_rejected_with_diagnostics()
    {
        var response = await _client.PostAsync("/api/convert", Form(SampleScore, """{"arrangement":{"defaultOctaveShift":9}}"""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var result = await ReadResultAsync(response);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.InvalidOption);
    }

    [Fact]
    public async Task Unreadable_score_is_rejected_with_diagnostics()
    {
        var response = await _client.PostAsync("/api/convert", Form("X:1\nK:C\n", options: null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains((await ReadResultAsync(response)).Diagnostics, d => d.Code == DiagnosticCodes.NoMusic);
    }

    [Theory]
    [InlineData("{oops", "Invalid options")]
    [InlineData("""{"arrangement":{"bass":{"pattern":"stride"}}}""", "Invalid options")]
    public async Task Malformed_options_are_a_bad_request(string options, string title)
    {
        var response = await _client.PostAsync("/api/convert", Form(SampleScore, options));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(title, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_file_is_a_bad_request()
    {
        var form = new MultipartFormDataContent { { new StringContent("{}"), "options" } };

        var response = await _client.PostAsync("/api/convert", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Oversized_file_is_a_bad_request()
    {
        var response = await _client.PostAsync("/api/convert", Form(new string('z', (int)ConvertEndpoints.MaxScoreBytes + 1), options: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static MultipartFormDataContent Form(string score, string? options, string fileName = "score.abc")
    {
        var file = new StringContent(score, Encoding.UTF8);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        var form = new MultipartFormDataContent { { file, "file", fileName } };
        if (options is not null)
        {
            form.Add(new StringContent(options), "options");
        }

        return form;
    }

    private static async Task<ConversionResult> ReadResultAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), YueToLogicJsonContext.Default.ConversionResult)!;
}
