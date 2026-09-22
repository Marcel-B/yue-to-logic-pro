using Microsoft.AspNetCore.Mvc;
using YueToLogic.Api.Data;
using YueToLogic.Api.Presets;

namespace YueToLogic.Api;

/// <summary>
/// The presets of the web form, kept on the server so that every browser the user opens the interface from
/// offers the same ones. A preset is addressed by its name, which is what the user knows it by; the server
/// keeps the form as opaque JSON.
/// </summary>
public static class PresetEndpoints
{
    public static RouteGroupBuilder MapPresetEndpoints(this RouteGroupBuilder api)
    {
        var presets = api.MapGroup("/presets");

        presets.MapGet("/", (IPresetStore store, ICurrentUser user) => Results.Ok(store.List(user.Id)))
            .WithName("ListPresets")
            .WithSummary("The user's presets, ordered by name; each carries the form as the web interface saved it.");

        presets.MapPut("/{name}", Save)
            .WithName("SavePreset")
            .WithSummary("Saves the form under the name, replacing a preset of that name: 201 when it is new, 200 otherwise.");

        presets.MapDelete("/{name}", Delete)
            .WithName("DeletePreset")
            .WithSummary("Removes the preset of that name.");

        return api;
    }

    private static IResult Save(string name, [FromBody] PresetInput input, IPresetStore store, ICurrentUser user)
    {
        if (input.Problems(name) is { Count: > 0 } problems)
        {
            return Results.Problem(title: "Invalid preset", detail: string.Join("; ", problems) + ".", statusCode: StatusCodes.Status400BadRequest);
        }

        var (preset, created) = store.Save(user.Id, name.Trim(), input.Form!.Value);
        return created ? Results.Created($"/api/presets/{Uri.EscapeDataString(preset.Name)}", preset) : Results.Ok(preset);
    }

    private static IResult Delete(string name, IPresetStore store, ICurrentUser user) =>
        store.Delete(user.Id, name.Trim())
            ? Results.NoContent()
            : Results.Problem(title: "Unknown preset", detail: $"There is no preset named '{name}'.", statusCode: StatusCodes.Status404NotFound);
}
