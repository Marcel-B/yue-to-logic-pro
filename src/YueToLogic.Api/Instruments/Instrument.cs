using System.Text.Json.Serialization;
using YueToLogic.Core.Arrangement;

namespace YueToLogic.Api.Instruments;

/// <summary>
/// A hardware instrument as the user knows it: a name for a MIDI port and channel, e.g. "Mother32" on
/// "MIDI4x4 Midi Out 1", channel 12. The port is the name Web MIDI shows in the browser, kept as text so that an
/// instrument survives its interface being unplugged. A drum machine is an instrument whose one channel plays
/// several drums, each on a note of its own; <see cref="Drums"/> says which.
/// </summary>
public sealed record Instrument(
    long Id,
    string Name,
    string Port,
    int Channel,
    InstrumentKind Kind = InstrumentKind.Synth,
    DrumNotes? Drums = null);

/// <summary>What an instrument is, which decides what the tracks that play it get from it.</summary>
/// <remarks>Spelled by name in JSON, as the library's own context spells enums, so every client reads one convention.</remarks>
[JsonConverter(typeof(JsonStringEnumConverter<InstrumentKind>))]
public enum InstrumentKind
{
    /// <summary>A synthesizer or sound module: the track goes to its port and channel, that is all.</summary>
    Synth,

    /// <summary>
    /// A drum machine or groove box: one channel, one drum per note. Tracks that play it also take its drum
    /// notes, so the kick region triggers its kick and not General MIDI's.
    /// </summary>
    DrumMachine,
}

/// <summary>What a client sends to create or change an instrument; everything is checked before it is stored.</summary>
/// <param name="Kind">Left out, the instrument is a synthesizer, as every instrument was before drum machines existed.</param>
/// <param name="Drums">
/// The drum machine's notes; left out, it gets the General MIDI ones. A synthesizer has none, whatever is sent.
/// </param>
public sealed record InstrumentInput(string? Name, string? Port, int? Channel, InstrumentKind? Kind = null, DrumNotes? Drums = null)
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

        if (Kind is { } kind && !Enum.IsDefined(kind))
        {
            problems.Add("kind must be Synth or DrumMachine");
        }

        if (Kind == InstrumentKind.DrumMachine && Drums is { } drums)
        {
            problems.AddRange(Enum.GetValues<Drum>()
                .Where(drum => drums.Of(drum) is < 0 or > 127)
                .Select(drum => $"drums.{DrumNotes.JsonName(drum)} must be a note between 0 and 127"));
        }

        return problems;
    }

    /// <summary>The values as they are stored: trimmed, the channel as a number, drums only for a drum machine. Only valid after <see cref="Problems"/> is empty.</summary>
    public InstrumentValues Cleaned()
    {
        var kind = Kind ?? InstrumentKind.Synth;
        return new InstrumentValues(Name!.Trim(), Port!.Trim(), Channel!.Value, kind, kind == InstrumentKind.DrumMachine ? Drums ?? new DrumNotes() : null);
    }
}

/// <summary>Everything an instrument has apart from its id: what the store is given to add or change one.</summary>
public sealed record InstrumentValues(string Name, string Port, int Channel, InstrumentKind Kind, DrumNotes? Drums)
{
    public Instrument WithId(long id) => new(id, Name, Port, Channel, Kind, Drums);
}

/// <summary>Which instrument a track plays; <c>null</c> takes the assignment away.</summary>
public sealed record TrackAssignmentInput(long? InstrumentId);
