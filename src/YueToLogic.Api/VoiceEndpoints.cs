using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using YueToLogic.Core.Serialization;
using YueToLogic.Core.Stems;
using YueToLogic.Core.Voices;

namespace YueToLogic.Api;

/// <summary>
/// Passes singing voice conversion on to ChangeMyVoice, which does the work on a Mac. The endpoints exist so
/// that the API key stays in this server's configuration: a browser never sees it, it only learns ids.
/// </summary>
/// <remarks>
/// Two things live at the service: the collection of reference voices, which is edited here like the
/// instrument library, and the jobs. A job's source is the vocal stem of a finished separation, which travels
/// from the stem service to this server and on to the voice service without the detour through the browser -
/// the browser passes the stem job's id, no audio. ChangeMyVoice has no interface of its own, so the list of
/// jobs is passed through as well; a service that cannot list them answers 501 here and the interface says so.
/// </remarks>
public static class VoiceEndpoints
{
    /// <summary>A reference voice is a few seconds of singing; the service keeps only the first 25 anyway.</summary>
    public const long MaxVoiceBytes = 64L * 1024 * 1024;

    public static RouteGroupBuilder MapVoiceEndpoints(this RouteGroupBuilder api)
    {
        var voice = api.MapGroup("/voice");

        voice.MapGet("/", (IServiceProvider services) => Results.Ok(new VoiceAvailability(Service(services) is not null)))
            .WithName("VoiceAvailable")
            .WithSummary("Says whether this server can have a voice changed at all.");

        voice.MapGet("/voices", ListVoicesAsync)
            .WithName("ReferenceVoices")
            .WithSummary("The collection of reference voices; one of their ids is what a job is started with.");

        voice.MapPost("/voices", AddVoiceAsync)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxVoiceBytes))
            .WithFormOptions(multipartBodyLengthLimit: MaxVoiceBytes)
            .WithName("AddReferenceVoice")
            .WithSummary("Stores a recording as a reference voice; the form fields are 'label' and 'file'.");

        voice.MapDelete("/voices/{id}", DeleteVoiceAsync)
            .WithName("DeleteReferenceVoice")
            .WithSummary("Removes a reference voice; the service refuses while a job still waits for it.");

        voice.MapPost("/jobs", StartJobAsync)
            .DisableAntiforgery()
            .WithName("StartVoiceJob")
            .WithSummary("Converts the vocals of a finished stem job to a reference voice; the form fields are 'voiceId' and 'stemJob'.");

        voice.MapGet("/jobs", ListJobsAsync)
            .WithName("VoiceJobs")
            .WithSummary("A page of the voice service's jobs, newest first, narrowed by 'status' and paged with 'limit' and 'offset'; 501 from a service that cannot list them.");

        voice.MapGet("/jobs/{id}", GetJobAsync)
            .WithName("VoiceJobStatus")
            .WithSummary("The state of a job: QUEUED, RUNNING, COMPLETED, FAILED or CANCELLED.");

        voice.MapGet("/jobs/{id}/result", DownloadAsync)
            .WithName("VoiceJobResult")
            .WithSummary("The converted recording as a WAV.");

        voice.MapDelete("/jobs/{id}", DeleteJobAsync)
            .WithName("DeleteVoiceJob")
            .WithSummary("Cancels a job or drops a finished one's result; repeatable.");

        return api;
    }

    /// <summary>
    /// The voice service is registered only when this server is configured for one, so it is looked up rather
    /// than injected: a missing dependency would otherwise fail the request instead of answering it.
    /// </summary>
    private static IVoiceConversionService? Service(IServiceProvider services) =>
        services.GetService<IVoiceConversionService>();

    private static async Task<IResult> ListVoicesAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        if (Service(services) is not { } service)
        {
            return Unavailable();
        }

        try
        {
            var voices = await service.ListVoicesAsync(cancellationToken).ConfigureAwait(false);
            return Results.Json(voices.ToArray(), YueToLogicJsonContext.Default.ReferenceVoiceArray);
        }
        catch (VoiceConversionException exception)
        {
            return Failed(exception);
        }
    }

    private static async Task<IResult> AddVoiceAsync(
        IFormFile? file,
        [FromForm] string? label,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (Service(services) is not { } service)
        {
            return Unavailable();
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            return BadRequest("Missing label", "Send the name of the voice as form field 'label'.");
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest("Missing recording", "Send the recording of the voice as form field 'file'.");
        }

        try
        {
            await using var audio = file.OpenReadStream();
            var voice = await service.AddVoiceAsync(label.Trim(), audio, file.FileName, cancellationToken).ConfigureAwait(false);
            return Results.Json(voice, YueToLogicJsonContext.Default.ReferenceVoice, statusCode: StatusCodes.Status201Created);
        }
        catch (VoiceConversionException exception)
        {
            return Failed(exception);
        }
    }

    private static async Task<IResult> DeleteVoiceAsync(string id, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (Service(services) is not { } service)
        {
            return Unavailable();
        }

        try
        {
            await service.DeleteVoiceAsync(id, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (VoiceConversionException exception)
        {
            return Failed(exception);
        }
    }

    /// <summary>
    /// Starts a conversion from the vocals of a finished separation. The dry vocals are preferred when the
    /// separation produced them: the model copies what it hears, and a hall that is sung along with stays in
    /// the result.
    /// </summary>
    private static async Task<IResult> StartJobAsync(
        [FromForm] string? voiceId,
        [FromForm] Guid? stemJob,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (Service(services) is not { } service)
        {
            return Unavailable();
        }

        if (string.IsNullOrWhiteSpace(voiceId))
        {
            return BadRequest("Missing voice", "Send the id of the reference voice as form field 'voiceId'.");
        }

        if (stemJob is not { } separation)
        {
            return BadRequest("Missing vocals", "Send the id of a finished stem job as form field 'stemJob'; its vocals are what gets converted.");
        }

        var stemService = services.GetService<IStemSeparationService>();
        if (stemService is null)
        {
            return Results.Problem(
                title: "Stem separation not configured",
                detail: "The vocals come from a stem job, and this server has no stem service.",
                statusCode: StatusCodes.Status501NotImplemented);
        }

        await using var stems = await StemImport.FetchAsync(stemService, separation, cancellationToken).ConfigureAwait(false);
        if ((stems.VocalsDry ?? stems.Vocals) is not { } vocals)
        {
            return Results.Problem(
                title: "No vocals to convert",
                detail: stems.Problem?.Message ?? "The separation holds no vocals.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        try
        {
            var job = await service.StartJobAsync(vocals, "vocals.wav", voiceId, settings: null, cancellationToken).ConfigureAwait(false);
            return Results.Json(job, YueToLogicJsonContext.Default.VoiceJob, statusCode: StatusCodes.Status202Accepted);
        }
        catch (VoiceConversionException exception)
        {
            return Failed(exception);
        }
    }

    private static async Task<IResult> GetJobAsync(string id, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (Service(services) is not { } service)
        {
            return Unavailable();
        }

        try
        {
            return Results.Json(await service.GetJobAsync(id, cancellationToken).ConfigureAwait(false), YueToLogicJsonContext.Default.VoiceJob);
        }
        catch (VoiceConversionException exception)
        {
            return Failed(exception);
        }
    }

    /// <summary>
    /// A page of the service's jobs. It keeps a record of every job it ever had, so the list only grows and
    /// the query is passed on as it came. Not every ChangeMyVoice offers this route; its 404 is not a job that
    /// is gone but a service that cannot answer, which the interface says instead of showing an empty list.
    /// </summary>
    private static async Task<IResult> ListJobsAsync(
        [FromQuery] string? status,
        [FromQuery] int? limit,
        [FromQuery] int? offset,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        if (Service(services) is not { } service)
        {
            return Unavailable();
        }

        try
        {
            var page = await service.ListJobsAsync(status, limit, offset, cancellationToken).ConfigureAwait(false);
            return Results.Json(page, YueToLogicJsonContext.Default.VoiceJobPage);
        }
        catch (VoiceConversionException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return Results.Problem(
                title: "Voice service cannot list its jobs",
                detail: "This ChangeMyVoice has no route for all of its jobs, so only the jobs started here can be followed.",
                statusCode: StatusCodes.Status501NotImplemented);
        }
        catch (VoiceConversionException exception)
        {
            return Failed(exception);
        }
    }

    private static async Task<IResult> DownloadAsync(string id, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (Service(services) is not { } service)
        {
            return Unavailable();
        }

        try
        {
            var result = await service.DownloadResultAsync(id, cancellationToken).ConfigureAwait(false);
            return Results.Stream(result, "audio/wav", $"voice-{id}.wav");
        }
        catch (VoiceConversionException exception)
        {
            return Failed(exception);
        }
    }

    private static async Task<IResult> DeleteJobAsync(string id, IServiceProvider services, CancellationToken cancellationToken)
    {
        if (Service(services) is not { } service)
        {
            return Unavailable();
        }

        try
        {
            await service.DeleteJobAsync(id, cancellationToken).ConfigureAwait(false);
            return Results.NoContent();
        }
        catch (VoiceConversionException exception)
        {
            return Failed(exception);
        }
    }

    /// <summary>
    /// The service's own refusal, handed on with its status so the client can tell them apart: something that
    /// is gone (404), a name that is taken or a voice a job still waits for (409) are answers, not failures.
    /// A rate limit (429) and a full queue (503) keep their status too, since those are worth trying again.
    /// </summary>
    private static IResult Failed(VoiceConversionException exception) =>
        Results.Problem(
            title: "Voice conversion failed",
            detail: exception.Message,
            statusCode: exception.StatusCode switch
            {
                HttpStatusCode.NotFound => StatusCodes.Status404NotFound,
                HttpStatusCode.Conflict => StatusCodes.Status409Conflict,
                HttpStatusCode.Gone => StatusCodes.Status410Gone,
                HttpStatusCode.TooManyRequests => StatusCodes.Status429TooManyRequests,
                HttpStatusCode.ServiceUnavailable => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status502BadGateway,
            });

    private static IResult Unavailable() =>
        Results.Problem(
            title: "Voice conversion not configured",
            detail: "This server has no voice service; set Voice:BaseUrl and Voice:ApiKey to use one.",
            statusCode: StatusCodes.Status501NotImplemented);

    private static IResult BadRequest(string title, string detail) =>
        Results.Problem(title: title, detail: detail, statusCode: StatusCodes.Status400BadRequest);
}

/// <param name="Available">Whether a voice service is configured; the interface hides the option without one.</param>
public sealed record VoiceAvailability(bool Available);
