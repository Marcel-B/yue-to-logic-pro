using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using YueToLogic.Api;
using YueToLogic.Core.Stems;
using YueToLogic.Core.Voices;

namespace YueToLogic.Api.Tests;

public class VoiceEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string JobId = "job-4711";

    private static readonly Guid StemJob = Guid.Parse("22222222-3333-4444-5555-666666666666");

    [Fact]
    public async Task Without_a_voice_service_the_interface_is_told_so()
    {
        // A developer's user secrets may configure a voice service; this test is about a server without one.
        var client = factory
            .WithWebHostBuilder(builder => builder.UseSetting("Voice:BaseUrl", "").UseSetting("Voice:ApiKey", ""))
            .CreateClient();

        var available = await client.GetFromJsonAsync<VoiceAvailability>("/api/voice");
        var voices = await client.GetAsync("/api/voice/voices");

        Assert.False(available!.Available);
        Assert.Equal(HttpStatusCode.NotImplemented, voices.StatusCode);
    }

    [Fact]
    public async Task The_collection_is_listed_added_to_and_thinned_out()
    {
        var voice = new FakeVoice();
        var client = Client(voice);

        var listed = await client.GetFromJsonAsync<ReferenceVoice[]>("/api/voice/voices");
        var added = await client.PostAsync("/api/voice/voices", VoiceForm("Marcel"));
        var removed = await client.DeleteAsync("/api/voice/voices/v1");

        Assert.Equal("Marcel Benders", Assert.Single(listed!).Label);
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Equal(("Marcel", "RIFFvoice"), (voice.Label, voice.Added));
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal("v1", voice.DeletedVoice);
    }

    [Fact]
    public async Task The_recording_of_a_voice_is_passed_on_to_listen_to()
    {
        var response = await Client(new FakeVoice()).GetAsync("/api/voice/voices/v1/audio");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("audio/wav", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("RIFFmaster v1", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Both answer 404: a voice that is gone names its code, a service without the route does not - and only
    /// the latter is something the interface explains as "cannot" rather than "not there".
    /// </summary>
    [Fact]
    public async Task A_service_without_the_route_is_told_apart_from_a_voice_that_is_gone()
    {
        var oldService = Client(new FakeVoice { Failure = new VoiceConversionException("not found", HttpStatusCode.NotFound) });
        var gone = Client(new FakeVoice { Failure = new VoiceConversionException("no such voice", HttpStatusCode.NotFound, VoiceConversionCode.VoiceNotFound) });

        var cannot = await oldService.GetAsync("/api/voice/voices/v1/audio");
        var missing = await gone.GetAsync("/api/voice/voices/v1/audio");

        Assert.Equal(HttpStatusCode.NotImplemented, cannot.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task A_voice_without_a_name_or_without_a_recording_is_refused_before_the_service_hears_of_it()
    {
        var voice = new FakeVoice();
        var client = Client(voice);

        var nameless = new MultipartFormDataContent { { new ByteArrayContent("RIFFvoice"u8.ToArray()), "file", "voice.wav" } };
        var empty = new MultipartFormDataContent { { new StringContent("Marcel"), "label" } };

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/voice/voices", nameless)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/voice/voices", empty)).StatusCode);
        Assert.Null(voice.Added);
    }

    /// <summary>
    /// The browser passes two ids and no audio: the vocal stem goes from the stem service to this server and
    /// on to the voice service. The dry vocals are preferred, since a hall that is sung along with stays in
    /// the converted result.
    /// </summary>
    [Fact]
    public async Task A_job_takes_the_vocals_of_a_separation_without_the_browser_seeing_them()
    {
        var voice = new FakeVoice();
        var client = Client(voice, new FakeStems());

        var response = await client.PostAsync("/api/voice/jobs", JobForm("v1", StemJob));
        var job = await response.Content.ReadFromJsonAsync<VoiceJob>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal((JobId, VoiceJobStatus.Queued), (job!.Id, job.Status));
        Assert.Equal("v1", voice.VoiceId);
        Assert.Equal("dry vocals", voice.Source);
    }

    /// <summary>
    /// Vocals the user brings along need no separation: the WAV goes to the service as it came, under its own
    /// name, and a server without a stem service takes it just the same.
    /// </summary>
    [Fact]
    public async Task A_job_takes_a_wav_of_vocals_without_a_separation()
    {
        var voice = new FakeVoice();

        var response = await Client(voice).PostAsync("/api/voice/jobs", WavForm("v1", "RIFF\0\0\0\0WAVEmy vocals", "Take 3.wav"));
        var job = await response.Content.ReadFromJsonAsync<VoiceJob>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(JobId, job!.Id);
        Assert.Equal(("v1", "RIFF\0\0\0\0WAVEmy vocals", "Take 3.wav"), (voice.VoiceId, voice.Source, voice.FileName));
    }

    [Fact]
    public async Task A_wav_is_refused_when_it_is_none_or_comes_with_a_separation()
    {
        var voice = new FakeVoice();
        var client = Client(voice, new FakeStems());

        var notWav = await client.PostAsync("/api/voice/jobs", WavForm("v1", "fLaC and more bytes", "vocals.wav"));
        var empty = await client.PostAsync("/api/voice/jobs", WavForm("v1", string.Empty, "vocals.wav"));
        var both = WavForm("v1", "RIFF\0\0\0\0WAVEmy vocals", "vocals.wav");
        both.Add(new StringContent(StemJob.ToString()), "stemJob");
        var twoSources = await client.PostAsync("/api/voice/jobs", both);

        Assert.Equal(HttpStatusCode.BadRequest, notWav.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, twoSources.StatusCode);
        Assert.Null(voice.Source);
    }

    [Fact]
    public async Task Without_a_finished_separation_there_is_nothing_to_convert()
    {
        var voice = new FakeVoice();
        var withoutStems = await Client(voice).PostAsync("/api/voice/jobs", JobForm("v1", StemJob));
        var withoutVoice = await Client(voice, new FakeStems()).PostAsync("/api/voice/jobs", JobForm(null, StemJob));
        var withoutJob = await Client(voice, new FakeStems()).PostAsync("/api/voice/jobs", JobForm("v1", null));

        // No stem service at all: the vocals have nowhere to come from.
        Assert.Equal(HttpStatusCode.NotImplemented, withoutStems.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withoutVoice.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withoutJob.StatusCode);
        Assert.Null(voice.Source);
    }

    [Fact]
    public async Task A_job_is_polled_downloaded_and_confirmed()
    {
        var voice = new FakeVoice();
        var client = Client(voice);

        var status = await client.GetFromJsonAsync<VoiceJob>($"/api/voice/jobs/{JobId}");
        var result = await client.GetAsync($"/api/voice/jobs/{JobId}/result");
        var confirmed = await client.DeleteAsync($"/api/voice/jobs/{JobId}");

        Assert.Equal(VoiceJobStatus.Completed, status!.Status);
        Assert.Equal("audio/wav", result.Content.Headers.ContentType!.MediaType);
        Assert.Equal("RIFFresult", await result.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        Assert.Equal(JobId, voice.DeletedJob);
    }

    /// <summary>
    /// The service keeps a record of every job it ever had, so the list is paged. What the interface asks for
    /// is handed on as it came, and the page is passed back with the total the paging is measured against.
    /// </summary>
    [Fact]
    public async Task A_page_of_jobs_is_passed_on_with_what_was_asked_for()
    {
        var created = new DateTimeOffset(2026, 9, 22, 8, 0, 0, TimeSpan.Zero);
        var voice = new FakeVoice
        {
            Jobs =
            [
                new VoiceJob(JobId, VoiceJobStatus.Completed, "v1", "Marcel", created, created.AddMinutes(1), created.AddMinutes(3), HasResult: true),
                new VoiceJob("job-0815", VoiceJobStatus.Failed, "v1", "Marcel", created.AddHours(-1), null, created, "MODEL_ERROR", "the Mac said no"),
            ],
            Total = 137,
        };

        var page = await Client(voice).GetFromJsonAsync<VoiceJobPage>("/api/voice/jobs?status=FAILED&limit=25&offset=50");

        Assert.Equal(voice.Jobs, page!.Jobs);
        Assert.Equal((137, 25, 50), (page.Total, page.Limit, page.Offset));
        Assert.Equal((VoiceJobStatus.Failed, 25, 50), (voice.Status, voice.Limit, voice.Offset));
    }

    [Fact]
    public async Task Without_a_query_the_service_pages_the_way_it_wants_to()
    {
        var voice = new FakeVoice();

        await Client(voice).GetFromJsonAsync<VoiceJobPage>("/api/voice/jobs");

        Assert.Equal((null, null, null), (voice.Status, voice.Limit, voice.Offset));
    }

    /// <summary>
    /// ChangeMyVoice does not list all of its jobs in every version. Its 404 is not a job that is gone but a
    /// route that is not there, which the interface explains instead of showing an empty list.
    /// </summary>
    [Fact]
    public async Task A_service_that_cannot_list_its_jobs_answers_not_implemented()
    {
        var client = Client(new FakeVoice { Failure = new VoiceConversionException("unknown", HttpStatusCode.NotFound) });

        var response = await client.GetAsync("/api/voice/jobs");

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    /// <summary>
    /// The service's own status is kept, since the interface acts on it: a name that is taken (409) is shown
    /// at the form, and a full queue (503) or a rate limit (429) is worth trying again, unlike the rest.
    /// </summary>
    [Fact]
    public async Task What_the_voice_service_refuses_is_passed_on_with_its_reason_and_its_status()
    {
        var busy = Client(new FakeVoice { Failure = new VoiceConversionException("the queue is full", HttpStatusCode.ServiceUnavailable, "QUEUE_FULL") });
        var limited = Client(new FakeVoice { Failure = new VoiceConversionException("rate limit", HttpStatusCode.TooManyRequests) });
        var taken = Client(new FakeVoice { Failure = new VoiceConversionException("a voice of that name is already there", HttpStatusCode.Conflict, "DUPLICATE_VOICE_LABEL") });
        var gone = Client(new FakeVoice { Failure = new VoiceConversionException("cleared away", HttpStatusCode.Gone, "RESULT_GONE") });
        var broken = Client(new FakeVoice { Failure = new VoiceConversionException("something else", HttpStatusCode.InternalServerError) });

        var queue = await busy.GetAsync($"/api/voice/jobs/{JobId}");
        var rate = await limited.GetAsync($"/api/voice/jobs/{JobId}");
        var duplicate = await taken.PostAsync("/api/voice/voices", VoiceForm("Marcel"));
        var cleared = await gone.GetAsync($"/api/voice/jobs/{JobId}/result");
        var failure = await broken.GetAsync($"/api/voice/jobs/{JobId}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, queue.StatusCode);
        Assert.Contains("the queue is full", await queue.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.TooManyRequests, rate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("name is already there", await duplicate.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Gone, cleared.StatusCode);
        // Anything the service did not name a status for is this server's problem with it.
        Assert.Equal(HttpStatusCode.BadGateway, failure.StatusCode);
    }

    /// <summary>
    /// A server with these two fakes and nothing else: a developer's user secrets may configure the real
    /// services, and a test about a server without a stem service would then talk to one.
    /// </summary>
    private HttpClient Client(IVoiceConversionService voice, IStemSeparationService? stems = null) =>
        factory
            .WithWebHostBuilder(builder => builder
                .UseSetting("Stems:BaseUrl", string.Empty)
                .UseSetting("Stems:ApiKey", string.Empty)
                .UseSetting("Voice:BaseUrl", string.Empty)
                .UseSetting("Voice:ApiKey", string.Empty)
                .ConfigureServices(services =>
                {
                    services.AddSingleton(voice);
                    if (stems is not null)
                    {
                        services.AddSingleton(stems);
                    }
                }))
            .CreateClient();

    private static MultipartFormDataContent VoiceForm(string label)
    {
        var file = new ByteArrayContent("RIFFvoice"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        return new MultipartFormDataContent { { new StringContent(label), "label" }, { file, "file", "voice.wav" } };
    }

    private static MultipartFormDataContent WavForm(string voiceId, string content, string fileName)
    {
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        return new MultipartFormDataContent { { new StringContent(voiceId), "voiceId" }, { file, "file", fileName } };
    }

    private static MultipartFormDataContent JobForm(string? voiceId, Guid? stemJob)
    {
        var form = new MultipartFormDataContent();
        if (voiceId is not null)
        {
            form.Add(new StringContent(voiceId), "voiceId");
        }

        if (stemJob is { } job)
        {
            form.Add(new StringContent(job.ToString()), "stemJob");
        }

        return form;
    }

    private sealed class FakeVoice : IVoiceConversionService
    {
        public VoiceConversionException? Failure { get; init; }

        public VoiceJob[] Jobs { get; init; } = [];

        /// <summary>How many jobs the service would have altogether, whatever this page holds.</summary>
        public int Total { get; init; }

        /// <summary>What the endpoint passed on of the query, so that nothing is invented on the way.</summary>
        public string? Status { get; private set; }

        public int? Limit { get; private set; }

        public int? Offset { get; private set; }

        /// <summary>What the endpoint passed on, so that what reaches the service can be checked.</summary>
        public string? Label { get; private set; }

        public string? Added { get; private set; }

        public string? VoiceId { get; private set; }

        public string? Source { get; private set; }

        public string? FileName { get; private set; }

        public string? DeletedVoice { get; private set; }

        public string? DeletedJob { get; private set; }

        public Task<IReadOnlyList<ReferenceVoice>> ListVoicesAsync(CancellationToken cancellationToken = default) =>
            Failure is null
                ? Task.FromResult<IReadOnlyList<ReferenceVoice>>([new ReferenceVoice("v1", "Marcel Benders")])
                : throw Failure;

        public async Task<ReferenceVoice> AddVoiceAsync(string label, Stream audio, string fileName, CancellationToken cancellationToken = default)
        {
            Label = label;
            Added = await new StreamReader(audio).ReadToEndAsync(cancellationToken);
            return Failure is null ? new ReferenceVoice("v2", label) : throw Failure;
        }

        public Task<Stream> DownloadVoiceAsync(string voiceId, CancellationToken cancellationToken = default) =>
            Failure is null ? Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes($"RIFFmaster {voiceId}"))) : throw Failure;

        public Task DeleteVoiceAsync(string voiceId, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            DeletedVoice = voiceId;
            return Task.CompletedTask;
        }

        public async Task<VoiceJob> StartJobAsync(
            Stream vocals,
            string fileName,
            string voiceId,
            VoiceConversionSettings? settings = null,
            CancellationToken cancellationToken = default)
        {
            VoiceId = voiceId;
            FileName = fileName;
            Source = await new StreamReader(vocals).ReadToEndAsync(cancellationToken);
            return Failure is null ? new VoiceJob(JobId, VoiceJobStatus.Queued, voiceId) : throw Failure;
        }

        public Task<VoiceJob> GetJobAsync(string jobId, CancellationToken cancellationToken = default) =>
            Failure is null ? Task.FromResult(new VoiceJob(jobId, VoiceJobStatus.Completed)) : throw Failure;

        public Task<VoiceJobPage> ListJobsAsync(
            string? status = null,
            int? limit = null,
            int? offset = null,
            CancellationToken cancellationToken = default)
        {
            Status = status;
            Limit = limit;
            Offset = offset;
            return Failure is null
                ? Task.FromResult(new VoiceJobPage(Jobs, Total, limit ?? 0, offset ?? 0))
                : throw Failure;
        }

        public Task<Stream> DownloadResultAsync(string jobId, CancellationToken cancellationToken = default) =>
            Failure is null ? Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("RIFFresult"))) : throw Failure;

        public Task DeleteJobAsync(string jobId, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            DeletedJob = jobId;
            return Task.CompletedTask;
        }
    }

    /// <summary>A stem service whose result tells the two vocal files apart by their content.</summary>
    private sealed class FakeStems : IStemSeparationService
    {
        public Task<StemJob> StartAsync(Stream flacAudio, bool dereverb = false, string? model = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StemJob(StemJob, StemJobStatus.Queued));

        public Task<IReadOnlyList<SeparationModel>> ListModelsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SeparationModel>>([]);

        public Task<StemJob> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(new StemJob(id, StemJobStatus.Completed));

        public Task<IReadOnlyList<StemJob>> ListAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StemJob>>([]);

        public Task<Stream> DownloadAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var buffer = new MemoryStream();
            using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (name, content) in new[] { ("vocals.wav", "wet vocals"), ("vocals_dry.wav", "dry vocals") })
                {
                    using var entry = new StreamWriter(archive.CreateEntry(name).Open());
                    entry.Write(content);
                }
            }

            buffer.Position = 0;
            return Task.FromResult<Stream>(buffer);
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
