namespace YueToLogic.Api.Presets;

/// <summary>
/// Keeps each user's presets. Every method takes the user, so that the store needs no change once there is
/// a login; until then the endpoints pass the one local user. The methods are synchronous because the store
/// is a local file whose queries take microseconds.
/// </summary>
public interface IPresetStore
{
    /// <summary>The user's presets, ordered by name.</summary>
    IReadOnlyList<Preset> List(string userId);

    /// <summary>Adds the preset, or replaces the one of that name (case-insensitively).</summary>
    /// <returns>The preset as stored, and whether it was new.</returns>
    (Preset Preset, bool Created) Save(string userId, string name, System.Text.Json.JsonElement form);

    /// <returns>Whether the user had a preset of that name.</returns>
    bool Delete(string userId, string name);
}
