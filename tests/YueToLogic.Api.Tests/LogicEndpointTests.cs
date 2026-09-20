using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using YueToLogic.Core.Diagnostics;
using YueToLogic.Core.Serialization;

namespace YueToLogic.Api.Tests;

public class LogicEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly string SampleScore = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Samples", "score.abc"));

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Score_and_audio_become_a_zipped_logic_project()
    {
        var response = await _client.PostAsync("/api/convert/logic", Form(SampleScore, Flac(1_047_273), name: "Mein Song"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/zip", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("Mein Song.logicx.zip", response.Content.Headers.ContentDisposition!.FileNameStar);

        using var archive = new ZipArchive(await response.Content.ReadAsStreamAsync());
        var entries = archive.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("Mein Song.logicx/Alternatives/000/ProjectData", entries);
        Assert.Contains("Mein Song.logicx/Media/Audio Files/audio.flac", entries);
        Assert.False(response.Headers.Contains(ConvertEndpoints.DiagnosticsHeader));
    }

    [Fact]
    public async Task Package_name_defaults_to_the_score_file_name_and_is_sanitized()
    {
        var withoutName = await _client.PostAsync("/api/convert/logic", Form(SampleScore, Flac(1_047_273)));
        var unsafeName = await _client.PostAsync("/api/convert/logic", Form(SampleScore, Flac(1_047_273), name: "../a/b:c"));

        Assert.Equal("score.logicx.zip", withoutName.Content.Headers.ContentDisposition!.FileNameStar);
        Assert.Equal("abc.logicx.zip", unsafeName.Content.Headers.ContentDisposition!.FileNameStar);
    }

    [Fact]
    public async Task Warnings_are_returned_in_a_header()
    {
        // Two minutes of audio for a 22-second score: probably the wrong file.
        var response = await _client.PostAsync("/api/convert/logic", Form(SampleScore, Flac(48_000 * 120)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var header = Assert.Single(response.Headers.GetValues(ConvertEndpoints.DiagnosticsHeader));
        var warnings = JsonSerializer.Deserialize(header, YueToLogicJsonContext.Default.DiagnosticArray)!;
        Assert.Contains(warnings, w => w.Code == DiagnosticCodes.AudioLengthMismatch);
    }

    [Fact]
    public async Task Audio_that_is_not_flac_is_rejected_with_diagnostics()
    {
        var response = await _client.PostAsync("/api/convert/logic", Form(SampleScore, "ID3 not a flac file"u8.ToArray()));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains(DiagnosticCodes.InvalidAudio, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Without_audio_the_project_is_written_with_an_empty_audio_track()
    {
        var response = await _client.PostAsync("/api/convert/logic", Form(SampleScore, audio: null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var archive = new ZipArchive(await response.Content.ReadAsStreamAsync());
        var entries = archive.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("score.logicx/Alternatives/000/ProjectData", entries);
        Assert.DoesNotContain(entries, e => e.EndsWith("audio.flac", StringComparison.Ordinal));
    }

    private static MultipartFormDataContent Form(string score, byte[]? audio, string? name = null)
    {
        var form = new MultipartFormDataContent();
        var scoreContent = new StringContent(score, Encoding.UTF8);
        scoreContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(scoreContent, "file", "score.abc");
        if (audio is not null)
        {
            var audioContent = new ByteArrayContent(audio);
            audioContent.Headers.ContentType = new MediaTypeHeaderValue("audio/flac");
            form.Add(audioContent, "audio", "audio.flac");
        }

        if (name is not null)
        {
            form.Add(new StringContent(name), "name");
        }

        return form;
    }

    /// <summary>A minimal 48 kHz stereo FLAC header followed by filler bytes.</summary>
    private static byte[] Flac(long samples)
    {
        var data = new byte[42 + 128];
        "fLaC"u8.CopyTo(data);
        data[4] = 0x80;
        data[7] = 34;
        var body = data.AsSpan(8, 34);
        body[10] = 48000 >> 12;
        body[11] = (48000 >> 4) & 0xFF;
        body[12] = (byte)(((48000 & 0x0F) << 4) | (1 << 1) | ((24 - 1) >> 4));
        body[13] = (byte)(((24 - 1) & 0x0F) << 4);
        BinaryPrimitives.WriteUInt32BigEndian(body[14..], (uint)samples);
        return data;
    }
}
