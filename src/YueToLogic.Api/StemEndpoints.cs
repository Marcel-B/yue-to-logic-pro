using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using YueToLogic.Core.Serialization;
using YueToLogic.Core.Stems;

namespace YueToLogic.Api;

/// <summary>
/// Passes stem separation on to StemMyWav, which does the work on a Mac. The endpoints exist so that the API
/// key stays in this server's configuration: a browser never sees it, it only learns the job id.
/// </summary>
/// <remarks>
/// Separation takes minutes, so the client starts a job, asks for its state now and then, and downloads the
/// result once it is done. Confirming the import with DELETE lets the service drop the files at once;
/// unconfirmed jobs are removed by it after a day. The service takes only a couple of waiting jobs, so the
/// list of all jobs is passed through as well: the stem service has no interface of its own, and this is
/// where a job that blocks the queue is found and removed.
/// </remarks>
public static class StemEndpoints
{
    /// <summary>What the stem service accepts, and more than a YuE recording ever is.</summary>
    public const long MaxAudioBytes = 512L * 1024 * 1024;

    public static RouteGroupBuilder MapStemEndpoints(this RouteGroupBuilder api)
    {
        var stems = api.MapGroup("/stems");

        stems.MapGet("/", (IServiceProvider services) => Results.Ok(new StemAvailability(Service(services) is not null)))
            .WithName("StemsAvailable")
            .WithSummary("Says whether this server can have stems separated at all.");

        stems.MapPost("/", StartAsync)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxAudioBytes))
            .WithName("StartStemJob")
            .WithSummary("Hands the audio.flac to the stem service; the body is the raw file.");

        stems.MapGet("/jobs", ListAsync)
            .WithName("StemJobs")
            .WithSummary("Every job the stem service knows, newest first; what is holding its queue up.");

        stems.MapGet("/{id:guid}", GetAsync)
            .WithName("StemJobStatus")
            .WithSummary("The state of a job: queued, processing, completed or failed.");

        stems.MapGet("/{id:guid}/result", DownloadAsync)
            .WithName("StemJobResult")
            .WithSummary("The finished stems as a ZIP of WAV files.");

        stems.MapDelete("/{id:guid}", DeleteAsync)
            .WithName("DeleteStemJob")
            .WithSummary("Confirms the import, whereupon the stem service removes the result; cancels a job that is still queued.");

        return api;
    }

    /// <summary>
    /// The stem service is registered only when this server is configured for one, so it is looked up rather
    /// than injected: a missing dependency would otherwise fail the request instead of answering it.
    /// </summary>
    private static IStemSeparationService? Service(IServiceProvider services) =>
        services.GetService<IStemSeparationService>();

    private static async Task<IResult> StartAsync(
        HttpRequest request,
        [FromQuery] bool? dereverb,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (Service(services) is not { } service)
        {
            return Unavailable();
        }

        if (request.ContentLength is 0 or null && !request.Body.CanRead)
        {
            return Results.Problem(title: "Missing audio", detail: "Send the audio.flac as the request body.", statusCode: StatusCodes.Status400BadRequest);
        }

        return await CallAsync(() => service.StartAsync(request.Body, dereverb ?? false, cancellationToken)).ConfigureAwait(false);
    }

    private static async Task<IResult> GetAsync(Guid id, IServiceProvider services, CancellationToken cancellationToken) =>
        Service(services) is not { } service
            ? Unavailable()
            : await CallAsync(() => service.GetAsync(id, cancellationToken)).ConfigureAwait(false);

    private static async Task<IResult> ListAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        if (Service(services) is not { } service)
        {
            return Unavailable();
        }

        try
        {
            var jobs = await service.ListAsync(cancellationToken).ConfigureAwait(false);
            return Results.Json(jobs.ToArray(), YueToLogicJsonContext.Default.StemJobArray);
        }
        catch (StemSeparationException exception)
        {
            return Failed(exception);
        }
    }

    private static async Task<IResult> DownloadAsync(Guid id, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (Service(services) is not { } service)
        {
            return Unavailable();
        }

        try
        {
            var stems = await service.DownloadAsync(id, cancellationToken).ConfigureAwait(false);
            return Results.Stream(stems, "application/zip", $"stems-{id}.zip");
        }
        catch (StemSeparationException exception)
        {
            return Failed(exception);
        }
    }

    private static async Task<IResult> DeleteAsync(Guid id, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (Service(services) is not { } service)
        {
            return Unavailable();
        }

        try
        {
            await service.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (StemSeparationException exception)
        {
            return Failed(exception);
        }
    }

    private static async Task<IResult> CallAsync(Func<Task<StemJob>> call)
    {
        try
        {
            return Results.Json(await call().ConfigureAwait(false), YueToLogicJsonContext.Default.StemJob);
        }
        catch (StemSeparationException exception)
        {
            return Failed(exception);
        }
    }

    /// <summary>
    /// The service's own refusal, handed on with its status so the client can tell them apart: a job that is
    /// gone (404) and one that cannot be removed while it is being transferred (409) are answers, not failures.
    /// </summary>
    private static IResult Failed(StemSeparationException exception) =>
        Results.Problem(
            title: "Stem separation failed",
            detail: exception.Message,
            statusCode: exception.StatusCode switch
            {
                HttpStatusCode.NotFound => StatusCodes.Status404NotFound,
                HttpStatusCode.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status502BadGateway,
            });

    private static IResult Unavailable() =>
        Results.Problem(
            title: "Stem separation not configured",
            detail: "This server has no stem service; set Stems:BaseUrl and Stems:ApiKey to use one.",
            statusCode: StatusCodes.Status501NotImplemented);
}

/// <param name="Available">Whether a stem service is configured; the interface hides the option without one.</param>
public sealed record StemAvailability(bool Available);
