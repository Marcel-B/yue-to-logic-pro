using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using YueToLogic.Core.Diagnostics;
using YueToLogic.Core.Serialization;
using YueToLogic.Core.Stems;
using YueToLogic.Core.Voices;

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

    [Fact]
    public async Task Splitting_at_sections_gives_every_track_one_region_per_section()
    {
        // Without options only the vocal and the chords carry notes, and the sample has two sections: those two
        // tracks get a region each per section, the nine empty ones keep their single region, plus the audio.
        var plain = await _client.PostAsync("/api/convert/logic", Form(SampleScore, Flac(1_047_273)));
        var split = await _client.PostAsync("/api/convert/logic", Form(SampleScore, Flac(1_047_273), splitSections: true));

        Assert.Equal(HttpStatusCode.OK, split.StatusCode);
        Assert.Equal(12, await PlacementCountAsync(plain));
        Assert.Equal(1 + (2 * 2) + 9, await PlacementCountAsync(split));
    }

    [Fact]
    public async Task An_instrument_names_the_logic_track_and_puts_it_on_the_instrument_channel()
    {
        const string instruments = """{"Bass":{"name":"Mother32","port":"MIDI4x4 Midi Out 1","channel":12}}""";
        var response = await _client.PostAsync("/api/convert/logic", Form(SampleScore, Flac(1_047_273), instruments: instruments));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var projectData = await ProjectDataAsync(response);
        Assert.Contains(Encoding.UTF8.GetBytes("Bass · Mother32"), projectData);
    }

    [Fact]
    public async Task Malformed_instruments_are_a_bad_request()
    {
        var notJson = await _client.PostAsync("/api/convert/logic", Form(SampleScore, Flac(1_047_273), instruments: "{not json"));
        var badChannel = await _client.PostAsync("/api/convert/logic", Form(SampleScore, Flac(1_047_273), instruments: """{"Bass":{"name":"Mother32","channel":17}}"""));

        Assert.Equal(HttpStatusCode.BadRequest, notJson.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badChannel.StatusCode);
        Assert.Contains("Bass", await badChannel.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    private static async Task<byte[]> ProjectDataAsync(HttpResponseMessage response)
    {
        using var archive = new ZipArchive(await response.Content.ReadAsStreamAsync());
        await using var stream = archive.Entries.Single(e => e.FullName.EndsWith("/Alternatives/000/ProjectData", StringComparison.Ordinal)).Open();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    /// <summary>The regions the project's arrangement holds: 80 bytes each, before its 16-byte terminator.</summary>
    private static async Task<int> PlacementCountAsync(HttpResponseMessage response)
    {
        using var archive = new ZipArchive(await response.Content.ReadAsStreamAsync());
        await using var stream = archive.Entries.Single(e => e.FullName.EndsWith("/Alternatives/000/ProjectData", StringComparison.Ordinal)).Open();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        var data = buffer.ToArray();

        // Chunks: a 24-byte file header, then a 36-byte header each; the arrangement is class 23, object 4.
        var offset = 24;
        while (offset < data.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(offset + 28, 8));
            var tag = new string(Encoding.ASCII.GetString(data, offset, 4).Reverse().ToArray());
            if (tag == "EvSq"
                && BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 6, 2)) == 23
                && BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 10, 4)) == 4)
            {
                return (length - 16) / 80;
            }

            offset += 36 + length;
        }

        throw new InvalidOperationException("The project has no arrangement.");
    }

    [Fact]
    public async Task The_stems_of_a_finished_job_go_into_the_project()
    {
        var stems = new FakeStems();
        var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IStemSeparationService>(stems)))
            .CreateClient();

        var response = await client.PostAsync("/api/convert/logic", Form(SampleScore, Flac(1_047_273), stemJob: FakeStems.Job));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var archive = new ZipArchive(await response.Content.ReadAsStreamAsync());
        var audio = archive.Entries.Select(e => e.FullName).Where(n => n.Contains("Audio Files/", StringComparison.Ordinal)).Order(StringComparer.Ordinal);
        Assert.Equal(
            ["score.logicx/Media/Audio Files/audio.flac", "score.logicx/Media/Audio Files/vocals.wav", "score.logicx/Media/Audio Files/vocals_dry.wav"],
            audio);

        // The stems are in the package, so the job was confirmed and the service may drop them.
        Assert.Equal(FakeStems.Job, stems.Deleted);
        Assert.False(response.Headers.Contains(ConvertEndpoints.DiagnosticsHeader));
    }

    [Fact]
    public async Task A_job_that_cannot_be_fetched_leaves_the_project_without_stems()
    {
        var stems = new FakeStems { Failure = new StemSeparationException("unknown job", HttpStatusCode.NotFound) };
        var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IStemSeparationService>(stems)))
            .CreateClient();

        var response = await client.PostAsync("/api/convert/logic", Form(SampleScore, Flac(1_047_273), stemJob: FakeStems.Job));

        // The project is still worth having, so the export carries on and says what happened.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var warnings = JsonSerializer.Deserialize(
            Assert.Single(response.Headers.GetValues(ConvertEndpoints.DiagnosticsHeader)),
            YueToLogicJsonContext.Default.DiagnosticArray)!;
        Assert.Contains(warnings, w => w.Code == DiagnosticCodes.StemsUnavailable);
        Assert.Null(stems.Deleted);

        using var archive = new ZipArchive(await response.Content.ReadAsStreamAsync());
        Assert.DoesNotContain(archive.Entries, e => e.Name == "vocals.wav");
    }

    [Fact]
    public async Task The_converted_vocals_of_a_voice_job_take_the_vocals_track()
    {
        var stems = new FakeStems();
        var voice = new FakeVoice(Wave(48000, 1, 16, 500_000));
        var client = Services(stems, voice);

        var response = await client.PostAsync(
            "/api/convert/logic",
            Form(SampleScore, Flac(1_047_273), stemJob: FakeStems.Job, voiceJob: FakeVoice.Job));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var archive = new ZipArchive(await response.Content.ReadAsStreamAsync());
        // The vocals track holds the converted recording - mono, where the separated stems are stereo - and
        // the separated dry vocals keep their own track.
        Assert.Equal(1, Channels(archive, "vocals.wav"));
        Assert.Equal(2, Channels(archive, "vocals_dry.wav"));

        // Both results are in the package now, so both services may drop them.
        Assert.Equal(FakeStems.Job, stems.Deleted);
        Assert.Equal(FakeVoice.Job, voice.Deleted);
        Assert.False(response.Headers.Contains(ConvertEndpoints.DiagnosticsHeader));
    }

    /// <summary>
    /// The Logic template's audio tracks are prepared for 48 kHz; ChangeMyVoice works at its model's own rate.
    /// A recording Logic cannot take must not fail an export that is otherwise fine.
    /// </summary>
    [Fact]
    public async Task Converted_vocals_of_another_sample_rate_leave_the_separated_ones_in_the_project()
    {
        var stems = new FakeStems();
        var voice = new FakeVoice(Wave(44100, 1, 16, 500_000));
        var client = Services(stems, voice);

        var response = await client.PostAsync(
            "/api/convert/logic",
            Form(SampleScore, Flac(1_047_273), stemJob: FakeStems.Job, voiceJob: FakeVoice.Job));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var warnings = JsonSerializer.Deserialize(
            Assert.Single(response.Headers.GetValues(ConvertEndpoints.DiagnosticsHeader)),
            YueToLogicJsonContext.Default.DiagnosticArray)!;
        Assert.Contains(warnings, w => w.Code == DiagnosticCodes.VoiceUnavailable);

        using var archive = new ZipArchive(await response.Content.ReadAsStreamAsync());
        // Still the separated vocals, which are stereo, rather than the converted mono recording.
        Assert.Equal(2, Channels(archive, "vocals.wav"));
        // Nothing was imported, so the result stays at the service and can still be fetched by hand.
        Assert.Null(voice.Deleted);
    }

    private HttpClient Services(IStemSeparationService stems, IVoiceConversionService voice) =>
        factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.AddSingleton(stems);
                services.AddSingleton(voice);
            }))
            .CreateClient();

    /// <summary>How many channels the WAV in the package has, which says which of two files ended up there.</summary>
    private static int Channels(ZipArchive archive, string name)
    {
        using var entry = Assert.Single(archive.Entries, e => e.Name == name).Open();
        var header = new byte[44];
        entry.ReadExactly(header);
        return BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(22));
    }

    /// <summary>A voice service with one finished job, whose result is what the test hands it.</summary>
    private sealed class FakeVoice(byte[] result) : IVoiceConversionService
    {
        public const string Job = "job-4711";

        public byte[] Result { get; } = result;

        public string? Deleted { get; private set; }

        public Task<IReadOnlyList<ReferenceVoice>> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ReferenceVoice>>([new ReferenceVoice("v1", "Marcel")]);

        public Task<ReferenceVoice> AddVoiceAsync(string label, Stream audio, string fileName, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ReferenceVoice("v1", label));

        public Task DeleteVoiceAsync(string voiceId, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<VoiceJob> StartJobAsync(
            Stream vocals,
            string fileName,
            string voiceId,
            VoiceConversionSettings? settings = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new VoiceJob(Job, VoiceJobStatus.Queued, voiceId));

        public Task<VoiceJob> GetJobAsync(string jobId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new VoiceJob(jobId, VoiceJobStatus.Completed));

        public Task<IReadOnlyList<VoiceJob>> ListJobsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VoiceJob>>([]);

        public Task<Stream> DownloadResultAsync(string jobId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(Result));

        public Task DeleteJobAsync(string jobId, CancellationToken cancellationToken = default)
        {
            Deleted = jobId;
            return Task.CompletedTask;
        }
    }

    /// <summary>A stem service whose result holds the two WAV files a project uses.</summary>
    private sealed class FakeStems : IStemSeparationService
    {
        public static readonly Guid Job = Guid.Parse("22222222-3333-4444-5555-666666666666");

        public StemSeparationException? Failure { get; init; }

        public Guid? Deleted { get; private set; }

        public Task<StemJob> StartAsync(Stream flacAudio, bool dereverb = false, string? model = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StemJob(Job, StemJobStatus.Queued));

        public Task<IReadOnlyList<SeparationModel>> ListModelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SeparationModel>>([new SeparationModel("htdemucs", "HTDemucs", IsDefault: true)]);

        public Task<StemJob> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StemJob(id, StemJobStatus.Completed));

        public Task<IReadOnlyList<StemJob>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StemJob>>([new StemJob(Job, StemJobStatus.Completed)]);

        public Task<Stream> DownloadAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            var buffer = new MemoryStream();
            using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var name in new[] { "vocals.wav", "instrumental.wav", "vocals_dry.wav" })
                {
                    using var entry = archive.CreateEntry(name).Open();
                    entry.Write(Wave(48000, 2, 16, 1_000_000));
                }
            }

            buffer.Position = 0;
            return Task.FromResult<Stream>(buffer);
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Deleted = id;
            return Task.CompletedTask;
        }
    }

    /// <summary>A WAVE header of the usual chunks, followed by a little silence.</summary>
    private static byte[] Wave(int sampleRate, int channels, int bitsPerSample, long samples)
    {
        var bytesPerFrame = channels * (bitsPerSample / 8);
        var data = (int)(samples * bytesPerFrame);
        var header = new byte[44];
        "RIFF"u8.CopyTo(header);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), (uint)(36 + data));
        "WAVE"u8.CopyTo(header.AsSpan(8));
        "fmt "u8.CopyTo(header.AsSpan(12));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(22), (ushort)channels);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(24), (uint)sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(28), (uint)(sampleRate * bytesPerFrame));
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(32), (ushort)bytesPerFrame);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(34), (ushort)bitsPerSample);
        "data"u8.CopyTo(header.AsSpan(36));
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(40), (uint)data);
        return [.. header, .. new byte[1024]];
    }

    private static MultipartFormDataContent Form(
        string score,
        byte[]? audio,
        string? name = null,
        bool? splitSections = null,
        Guid? stemJob = null,
        string? voiceJob = null,
        string? instruments = null)
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

        if (stemJob is { } job)
        {
            form.Add(new StringContent(job.ToString()), "stemJob");
        }

        if (voiceJob is not null)
        {
            form.Add(new StringContent(voiceJob), "voiceJob");
        }

        if (splitSections is { } split)
        {
            form.Add(new StringContent(split ? "true" : "false"), "splitSections");
        }

        if (instruments is not null)
        {
            form.Add(new StringContent(instruments, Encoding.UTF8), "instruments");
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
