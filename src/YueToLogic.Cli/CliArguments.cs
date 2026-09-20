using System.Globalization;
using YueToLogic.Core.Abc;
using YueToLogic.Core.Arrangement;
using YueToLogic.Core.Conversion;

namespace YueToLogic.Cli;

internal sealed record CliArguments
{
    public string InputPath { get; init; } = string.Empty;

    public string? OutputPath { get; init; }

    public string? JsonPath { get; init; }

    /// <summary>YuE audio.flac of a Logic Pro project written with audio.</summary>
    public string? LogicAudioPath { get; init; }

    /// <summary>Whether a Logic Pro project is written next to the MIDI file, with or without audio.</summary>
    public bool WriteLogicProject { get; init; }

    public bool IncludeChords { get; init; } = true;

    public int TicksPerQuarterNote { get; init; } = AbcParseOptions.DefaultTicksPerQuarterNote;

    /// <summary>Octave shift for both voices; <see cref="VocalOctave"/> and <see cref="InsOctave"/> take precedence.</summary>
    public int Octave { get; init; }

    public int? VocalOctave { get; init; }

    public int? InsOctave { get; init; }

    public BassPattern? Bass { get; init; }

    public int BassOctave { get; init; }

    public DrumPattern? Drums { get; init; }

    /// <summary>How the chord symbols are played; without it they sound as written, as block chords.</summary>
    public ChordPattern? Chords { get; init; }

    public bool Force { get; init; }

    public bool Verbose { get; init; }

    public bool ShowHelp { get; init; }

    /// <returns><c>false</c> with an <paramref name="error"/> message if the arguments are invalid.</returns>
    public static bool TryParse(string[] args, CliText text, out CliArguments result, out string? error)
    {
        result = new CliArguments();
        error = null;
        string? input = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "-h" or "--help":
                    result = result with { ShowHelp = true };
                    return true;
                case "-o" or "--output":
                    if (!TryTakeValue(args, ref i, text, out var output, out error))
                    {
                        return false;
                    }

                    result = result with { OutputPath = output };
                    break;
                case "--dump-json":
                    if (!TryTakeValue(args, ref i, text, out var json, out error))
                    {
                        return false;
                    }

                    result = result with { JsonPath = json };
                    break;
                case "--logic":
                    if (!TryTakeValue(args, ref i, text, out var audio, out error))
                    {
                        return false;
                    }

                    result = result with { LogicAudioPath = audio, WriteLogicProject = true };
                    break;
                case "--logic-no-audio":
                    result = result with { WriteLogicProject = true };
                    break;
                case "--ppq":
                    if (!TryTakeValue(args, ref i, text, out var ppqText, out error))
                    {
                        return false;
                    }

                    if (!int.TryParse(ppqText, NumberStyles.None, CultureInfo.InvariantCulture, out var ppq) || ppq is < ConversionOptionsValidator.MinTicksPerQuarterNote or > ConversionOptionsValidator.MaxTicksPerQuarterNote)
                    {
                        error = text.Format(text.InvalidPpq, ppqText);
                        return false;
                    }

                    result = result with { TicksPerQuarterNote = ppq };
                    break;
                case "--octave" or "--vocal-octave" or "--ins-octave" or "--bass-octave":
                    if (!TryTakeValue(args, ref i, text, out var octaveText, out error))
                    {
                        return false;
                    }

                    var maxOctaves = arg == "--bass-octave" ? ConversionOptionsValidator.MaxBassOctaveShift : ConversionOptionsValidator.MaxOctaveShift;
                    if (!int.TryParse(octaveText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var octaves)
                        || octaves < -maxOctaves
                        || octaves > maxOctaves)
                    {
                        error = text.Format(text.InvalidOctave, arg, octaveText, maxOctaves);
                        return false;
                    }

                    result = arg switch
                    {
                        "--vocal-octave" => result with { VocalOctave = octaves },
                        "--ins-octave" => result with { InsOctave = octaves },
                        "--bass-octave" => result with { BassOctave = octaves, Bass = result.Bass ?? BassPattern.Eighths },
                        _ => result with { Octave = octaves },
                    };
                    break;
                case "--bass":
                    result = result with { Bass = result.Bass ?? BassPattern.Eighths };
                    break;
                case "--bass-pattern":
                    if (!TryTakeValue(args, ref i, text, out var patternText, out error))
                    {
                        return false;
                    }

                    if (!BassPatterns.TryGetValue(patternText, out var pattern))
                    {
                        error = text.Format(text.InvalidBassPattern, patternText, string.Join(", ", BassPatterns.Keys));
                        return false;
                    }

                    result = result with { Bass = pattern };
                    break;
                case "--drums":
                    result = result with { Drums = result.Drums ?? DrumPattern.FourOnTheFloor };
                    break;
                case "--drum-pattern":
                    if (!TryTakeValue(args, ref i, text, out var drumText, out error))
                    {
                        return false;
                    }

                    if (!DrumPatterns.TryGetValue(drumText, out var drumPattern))
                    {
                        error = text.Format(text.InvalidDrumPattern, drumText, string.Join(", ", DrumPatterns.Keys));
                        return false;
                    }

                    result = result with { Drums = drumPattern };
                    break;
                case "--chord-pattern":
                    if (!TryTakeValue(args, ref i, text, out var chordText, out error))
                    {
                        return false;
                    }

                    if (!ChordPatterns.TryGetValue(chordText, out var chordPattern))
                    {
                        error = text.Format(text.InvalidChordPattern, chordText, string.Join(", ", ChordPatterns.Keys));
                        return false;
                    }

                    result = result with { Chords = chordPattern };
                    break;
                case "--no-chords":
                    result = result with { IncludeChords = false };
                    break;
                case "-f" or "--force":
                    result = result with { Force = true };
                    break;
                case "-v" or "--verbose":
                    result = result with { Verbose = true };
                    break;
                default:
                    if (arg.StartsWith('-') && arg.Length > 1)
                    {
                        error = text.Format(text.UnknownOption, arg);
                        return false;
                    }

                    if (input is not null)
                    {
                        error = text.Format(text.UnexpectedArgument, arg);
                        return false;
                    }

                    input = arg;
                    break;
            }
        }

        if (input is null)
        {
            error = text.MissingInput;
            return false;
        }

        result = result with { InputPath = input };
        return true;
    }

    public ArrangementOptions ToArrangementOptions()
    {
        var shifts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (VocalOctave is { } vocal)
        {
            shifts["Vocal"] = vocal;
        }

        if (InsOctave is { } ins)
        {
            shifts["Ins"] = ins;
        }

        return new ArrangementOptions
        {
            DefaultOctaveShift = Octave,
            OctaveShifts = shifts,
            Bass = Bass is { } pattern ? new BassOptions { Pattern = pattern, OctaveShift = BassOctave } : null,
            Drums = Drums is { } drums ? new DrumOptions { Pattern = drums } : null,
            Chords = Chords is { } chords ? new ChordOptions { Pattern = chords } : null,
        };
    }

    private static readonly Dictionary<string, BassPattern> BassPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eighths"] = BassPattern.Eighths,
        ["quarters"] = BassPattern.Quarters,
        ["root-fifth"] = BassPattern.RootFifth,
        ["octaves"] = BassPattern.Octaves,
        ["offbeat"] = BassPattern.Offbeat,
        ["sustained"] = BassPattern.Sustained,
    };

    private static readonly Dictionary<string, ChordPattern> ChordPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["block"] = ChordPattern.Block,
        ["eighths"] = ChordPattern.Eighths,
        ["offbeat"] = ChordPattern.Offbeat,
        ["arpeggio"] = ChordPattern.ArpeggioUp,
    };

    private static readonly Dictionary<string, DrumPattern> DrumPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["four-on-the-floor"] = DrumPattern.FourOnTheFloor,
        ["backbeat"] = DrumPattern.Backbeat,
        ["half-time"] = DrumPattern.HalfTime,
        ["disco"] = DrumPattern.Disco,
    };

    private static bool TryTakeValue(string[] args, ref int i, CliText text, out string value, out string? error)
    {
        if (i + 1 >= args.Length)
        {
            value = string.Empty;
            error = text.Format(text.MissingValue, args[i]);
            return false;
        }

        value = args[++i];
        error = null;
        return true;
    }
}
