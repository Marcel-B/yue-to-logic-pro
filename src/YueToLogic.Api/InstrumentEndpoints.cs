using Microsoft.AspNetCore.Mvc;
using YueToLogic.Api.Instruments;

namespace YueToLogic.Api;

/// <summary>
/// The instrument library: named MIDI port and channel combinations, and which track plays which. Both live
/// on the server so that every browser the user opens the interface from sees the same setup.
/// </summary>
public static class InstrumentEndpoints
{
    /// <summary>Track names are the template's ("Vocal 8vb"); anything longer is not one.</summary>
    public const int MaxTrackLength = 64;

    public static RouteGroupBuilder MapInstrumentEndpoints(this RouteGroupBuilder api)
    {
        var instruments = api.MapGroup("/instruments");

        instruments.MapGet("/", (IInstrumentStore store) => Results.Ok(store.List()))
            .WithName("ListInstruments")
            .WithSummary("All instruments, ordered by name.");

        instruments.MapPost("/", Create)
            .WithName("CreateInstrument")
            .WithSummary("Adds an instrument: a name for a MIDI port and channel (1-16). The name must be new.");

        instruments.MapPut("/{id:long}", Update)
            .WithName("UpdateInstrument")
            .WithSummary("Changes an instrument's name, port or channel.");

        instruments.MapDelete("/{id:long}", Delete)
            .WithName("DeleteInstrument")
            .WithSummary("Removes an instrument and the assignments of tracks to it.");

        instruments.MapGet("/assignments", (IInstrumentStore store) => Results.Ok(store.Assignments()))
            .WithName("ListAssignments")
            .WithSummary("Which instrument each track plays, as track name → instrument id.");

        instruments.MapPut("/assignments/{track}", Assign)
            .WithName("AssignInstrument")
            .WithSummary("Gives a track an instrument, or takes it away with an instrumentId of null.");

        return api;
    }

    private static IResult Create([FromBody] InstrumentInput input, IInstrumentStore store)
    {
        if (input.Problems() is { Count: > 0 } problems)
        {
            return Invalid(problems);
        }

        var (name, port, channel) = input.Cleaned();
        try
        {
            var instrument = store.Add(name, port, channel);
            return Results.Created($"/api/instruments/{instrument.Id}", instrument);
        }
        catch (DuplicateInstrumentNameException exception)
        {
            return Taken(exception);
        }
    }

    private static IResult Update(long id, [FromBody] InstrumentInput input, IInstrumentStore store)
    {
        if (input.Problems() is { Count: > 0 } problems)
        {
            return Invalid(problems);
        }

        var (name, port, channel) = input.Cleaned();
        try
        {
            return store.Update(id, name, port, channel) is { } instrument ? Results.Ok(instrument) : NotFound(id);
        }
        catch (DuplicateInstrumentNameException exception)
        {
            return Taken(exception);
        }
    }

    private static IResult Delete(long id, IInstrumentStore store) =>
        store.Delete(id) ? Results.NoContent() : NotFound(id);

    private static IResult Assign(string track, [FromBody] TrackAssignmentInput input, IInstrumentStore store)
    {
        var name = track.Trim();
        if (name.Length is 0 or > MaxTrackLength)
        {
            return Results.Problem(
                title: "Invalid track",
                detail: $"A track name has between 1 and {MaxTrackLength} characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (input.InstrumentId is not { } id)
        {
            store.Unassign(name);
            return Results.NoContent();
        }

        return store.Assign(name, id) ? Results.NoContent() : NotFound(id);
    }

    private static IResult Invalid(IReadOnlyList<string> problems) =>
        Results.Problem(title: "Invalid instrument", detail: string.Join("; ", problems) + ".", statusCode: StatusCodes.Status400BadRequest);

    private static IResult Taken(DuplicateInstrumentNameException exception) =>
        Results.Problem(title: "Name taken", detail: exception.Message, statusCode: StatusCodes.Status409Conflict);

    private static IResult NotFound(long id) =>
        Results.Problem(title: "Unknown instrument", detail: $"There is no instrument with id {id}.", statusCode: StatusCodes.Status404NotFound);
}
