using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using YueToLogic.Api;
using YueToLogic.Core.Conversion;
using YueToLogic.Core.Diagnostics;
using YueToLogic.Core.Serialization;

namespace YueToLogic.Api.Tests;

public class ConvertEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
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
    [InlineData("""{"arrangement":{"bass":{"pattern":"walking"}}}""", "Invalid options")]
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
