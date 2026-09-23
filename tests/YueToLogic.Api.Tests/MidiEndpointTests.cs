using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using YueToLogic.Core.Conversion;
using YueToLogic.Core.Diagnostics;
using YueToLogic.Core.Serialization;

namespace YueToLogic.Api.Tests;

public class MidiEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string SampleScore = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", "score.abc")).ReplaceLineEndings("\n");

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task The_midi_file_of_a_conversion_comes_back_as_its_score()
    {
        var response = await _client.PostAsync("/api/midi/abc", Form(SampleMidi(), null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadResultAsync(response);
        Assert.Equal(SampleScore.TrimEnd('\n'), result.Abc!.TrimEnd('\n'));
        Assert.Equal(["Vocal", "Chords"], result.Tracks.Select(t => t.Name));
        Assert.Equal([MidiTrackRole.Vocal, MidiTrackRole.Chords], result.Tracks.Select(t => t.Role));
    }

    [Fact]
    public async Task Roles_and_bars_to_leave_out_come_as_options()
    {
        // The chords left out, and the first bar with them.
        const string options = """{"trackRoles":{"1":"ignore"},"skipBars":1}""";

        var response = await _client.PostAsync("/api/midi/abc", Form(SampleMidi(), options));

        var result = await ReadResultAsync(response);
        Assert.True(result.Success);
        Assert.Equal(MidiTrackRole.Ignore, result.Tracks[1].Role);
        Assert.Empty(result.Score!.Chords);
        Assert.Equal(7 * 4 * 480, result.Score.LengthTicks);
    }

    [Fact]
    public async Task A_file_that_is_no_midi_file_is_answered_with_its_diagnostics()
    {
        var response = await _client.PostAsync("/api/midi/abc", Form("X:1\nK:C\n"u8.ToArray(), null));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var result = await ReadResultAsync(response);
        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.MidiUnreadable, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task Missing_file_and_broken_options_are_bad_requests()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsync("/api/midi/abc", new MultipartFormDataContent())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsync("/api/midi/abc", Form(SampleMidi(), "{"))).StatusCode);
    }

    private static byte[] SampleMidi() => new ScoreConverter().Convert(SampleScore).Midi!;

    private static MultipartFormDataContent Form(byte[] midi, string? options)
    {
        var file = new ByteArrayContent(midi);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/midi");
        var form = new MultipartFormDataContent { { file, "file", "song.mid" } };
        if (options is not null)
        {
            form.Add(new StringContent(options), "options");
        }

        return form;
    }

    private static async Task<MidiToAbcResult> ReadResultAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize(await response.Content.ReadAsStringAsync(), YueToLogicJsonContext.Default.MidiToAbcResult)!;
}
