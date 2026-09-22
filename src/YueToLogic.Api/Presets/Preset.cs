using System.Text.Json;

namespace YueToLogic.Api.Presets;

/// <summary>
/// A named set of the web form's parameters, so that a song can be converted the same way again without
/// clicking through every option. The form is kept as the JSON the browser holds and handed back as is:
/// the server does not interpret it, so an option added to the form needs nothing here.
/// </summary>
/// <param name="UpdatedAt">When the preset was last saved, ISO 8601 in UTC.</param>
public sealed record Preset(long Id, string Name, JsonElement Form, string UpdatedAt);

/// <summary>What a client sends to save a preset under a name; the name is in the URL.</summary>
public sealed record PresetInput(JsonElement? Form)
{
    public const int MaxNameLength = 64;

    /// <summary>A form is a few hundred bytes; anything near this is not one.</summary>
    public const int MaxFormBytes = 64 * 1024;

    /// <summary>What is wrong with the name or the form, in words a client can show; empty when it can be stored.</summary>
    public IReadOnlyList<string> Problems(string name)
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            problems.Add("name must not be empty");
        }
        else if (name.Trim().Length > MaxNameLength)
        {
            problems.Add($"name may have at most {MaxNameLength} characters");
        }

        if (Form is not { ValueKind: JsonValueKind.Object } form)
        {
            problems.Add("form must be a JSON object");
        }
        else if (form.GetRawText().Length > MaxFormBytes)
        {
            problems.Add($"form may have at most {MaxFormBytes} bytes");
        }

        return problems;
    }
}
