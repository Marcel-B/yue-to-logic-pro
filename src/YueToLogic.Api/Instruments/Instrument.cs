namespace YueToLogic.Api.Instruments;

/// <summary>
/// A hardware instrument as the user knows it: a name for a MIDI port and channel, e.g. "Mother32" on
/// "MIDI4x4 Midi Out 1", channel 12. The port is the name Web MIDI shows in the browser, kept as text so that an
/// instrument survives its interface being unplugged.
/// </summary>
public sealed record Instrument(long Id, string Name, string Port, int Channel);

/// <summary>What a client sends to create or change an instrument; everything is checked before it is stored.</summary>
public sealed record InstrumentInput(string? Name, string? Port, int? Channel)
{
    public const int MaxNameLength = 64;

    /// <summary>CoreMIDI allows 64 bytes for a port and 32 for a device; Web MIDI joins them with a space.</summary>
    public const int MaxPortLength = 128;

    /// <summary>What is wrong with the input, in words a client can show; empty when it can be stored.</summary>
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(Name))
        {
            problems.Add("name must not be empty");
        }
        else if (Name.Trim().Length > MaxNameLength)
        {
            problems.Add($"name may have at most {MaxNameLength} characters");
        }

        if (string.IsNullOrWhiteSpace(Port))
        {
            problems.Add("port must not be empty");
        }
        else if (Port.Trim().Length > MaxPortLength)
        {
            problems.Add($"port may have at most {MaxPortLength} characters");
        }

        if (Channel is not (>= 1 and <= 16))
        {
            problems.Add("channel must be between 1 and 16");
        }

        return problems;
    }

    /// <summary>The values as they are stored: trimmed, and the channel as a number. Only valid after <see cref="Problems"/> is empty.</summary>
    public (string Name, string Port, int Channel) Cleaned() => (Name!.Trim(), Port!.Trim(), Channel!.Value);
}

/// <summary>Which instrument a track plays; <c>null</c> takes the assignment away.</summary>
public sealed record TrackAssignmentInput(long? InstrumentId);
