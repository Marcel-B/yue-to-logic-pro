using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using YueToLogic.Api;
using YueToLogic.Core.Stems;

namespace YueToLogic.Api.Tests;

public class StemEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly Guid JobId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public async Task Without_a_stem_service_the_interface_is_told_so()
    {
        // A developer's user secrets may configure a stem service; this test is about a server without one.
        var client = factory
            .WithWebHostBuilder(builder => builder.UseSetting("Stems:BaseUrl", "").UseSetting("Stems:ApiKey", ""))
            .CreateClient();

        var available = await client.GetFromJsonAsync<StemAvailability>("/api/stems");
        var started = await client.PostAsync("/api/stems", Flac());

        Assert.False(available!.Available);
        Assert.Equal(HttpStatusCode.NotImplemented, started.StatusCode);
    }

    [Fact]
    public async Task A_job_is_started_polled_downloaded_and_confirmed()
    {
        var stems = new FakeStems();
        var client = Client(stems);

        var started = await client.PostAsync("/api/stems?dereverb=true", Flac());
        var job = await started.Content.ReadFromJsonAsync<StemJob>();
        var status = await client.GetFromJsonAsync<StemJob>($"/api/stems/{JobId}");
        var result = await client.GetAsync($"/api/stems/{JobId}/result");
        var confirmed = await client.DeleteAsync($"/api/stems/{JobId}");

        Assert.Equal(JobId, job!.Id);
        Assert.True(stems.Dereverb); // the switch reaches the service
        Assert.Equal("fLaC", stems.Audio); // the body is handed on unchanged
        Assert.Equal(StemJobStatus.Completed, status!.Status);
        Assert.Equal("application/zip", result.Content.Headers.ContentType!.MediaType);
        Assert.Equal("PK-stems", await result.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, confirmed.StatusCode);
        Assert.Equal(JobId, stems.Deleted);
    }

    [Fact]
    public async Task What_the_stem_service_refuses_is_passed_on_with_its_reason()
    {
        var client = Client(new FakeStems { Failure = new StemSeparationException("the queue is full", HttpStatusCode.TooManyRequests) });

        var response = await client.GetAsync($"/api/stems/{JobId}");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("the queue is full", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_job_the_service_does_not_know_is_a_not_found()
    {
        var client = Client(new FakeStems { Failure = new StemSeparationException("unknown job", HttpStatusCode.NotFound) });

        var response = await client.GetAsync($"/api/stems/{JobId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient Client(IStemSeparationService stems) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton(stems))).CreateClient();

    private static StreamContent Flac()
    {
        var content = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("fLaC")));
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/flac");
        return content;
    }

    private sealed class FakeStems : IStemSeparationService
    {
        public StemSeparationException? Failure { get; init; }

        public bool Dereverb { get; private set; }

        public string? Audio { get; private set; }

        public Guid? Deleted { get; private set; }

        public async Task<StemJob> StartAsync(Stream flacAudio, bool dereverb = false, CancellationToken cancellationToken = default)
        {
            Dereverb = dereverb;
            Audio = await new StreamReader(flacAudio).ReadToEndAsync(cancellationToken);
            return Failure is null ? new StemJob(JobId, StemJobStatus.Queued) : throw Failure;
        }

        public Task<StemJob> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Failure is null ? Task.FromResult(new StemJob(id, StemJobStatus.Completed, 1)) : throw Failure;

        public Task<Stream> DownloadAsync(Guid id, CancellationToken cancellationToken = default) =>
            Failure is null ? Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes("PK-stems"))) : throw Failure;

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Deleted = id;
            return Failure is null ? Task.CompletedTask : throw Failure;
        }
    }
}
