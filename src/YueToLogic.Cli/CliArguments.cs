using System.Globalization;
using YueToLogic.Core.Abc;
using YueToLogic.Core.Arrangement;
using YueToLogic.Core.Conversion;
using YueToLogic.Core.Harmony;

namespace YueToLogic.Cli;

internal sealed record CliArguments
{
    public string InputPath { get; init; } = string.Empty;

    public string? OutputPath { get; init; }

    public string? JsonPath { get; init; }

    /// <summary>YuE audio.flac of a Logic Pro project written with audio.</summary>
    public string? LogicAudioPath { get; init; }

    /// <summary>Separated vocals for the Logic project's second audio track.</summary>
    public string? VocalsPath { get; init; }

    /// <summary>Separated vocals without their reverb, for the third audio track.</summary>
    public string? VocalsDryPath { get; init; }

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

    /// <summary>Inversion the chord track is voiced in; without it every chord stands on its root.</summary>
    public ChordInversion? Inversion { get; init; }

    public int ChordOctave { get; init; }

    /// <summary>Crash cymbal at the start of every section; only has an effect together with a drum track.</summary>
    public bool Crash { get; init; } = true;

    /// <summary>One track per drum instead of one drum track.</summary>
    public bool SplitDrums { get; init; }

    /// <summary>
    /// The notes of a drum machine, from <c>--drum-note kick=C1</c>; drums not named keep their General MIDI
    /// note. <c>null</c> until the first such flag, so the options say nothing about notes without one.
    /// </summary>
    public DrumNotes? DrumNotes { get; init; }

    public bool GuideTones { get; init; }

    public int GuideOctave { get; init; }

    public bool DoubleVocal { get; init; }

    public int DoubleOctave { get; init; } = -1;

    /// <summary>Swing in percent: 0 straight, 100 a full triplet feel.</summary>
    public int SwingPercent { get; init; }

    public SwingUnit SwingUnit { get; init; } = SwingUnit.Eighths;

    /// <summary>Keeps the drums on the grid while everything else swings.</summary>
    public bool StraightDrums { get; init; }

    /// <summary>Humanization in percent of <see cref="MaxHumanizeTimingMs"/> and <see cref="MaxHumanizeVelocity"/>.</summary>
    public int HumanizePercent { get; init; }

    public bool Mono { get; init; }

    public bool Legato { get; init; }

    /// <summary>One region per song section in the Logic project instead of one per track.</summary>
    public bool SplitSections { get; init; }

    /// <summary>Silent bars in front of the song; 0 starts at bar 1.</summary>
    public int CountIn { get; init; }

    /// <summary>A click on every beat of the count-in.</summary>
    public bool CountInClick { get; init; } = true;

    /// <summary>Fits the tempo to the length of the audio given with <c>--logic</c>.</summary>
    public bool FitTempo { get; init; }

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
                case "--vocals" or "--vocals-dry":
                    if (!TryTakeValue(args, ref i, text, out var stem, out error))
                    {
                        return false;
                    }

                    result = arg == "--vocals"
                        ? result with { VocalsPath = stem, WriteLogicProject = true }
                        : result with { VocalsDryPath = stem, WriteLogicProject = true };
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
                case "--octave" or "--vocal-octave" or "--ins-octave" or "--bass-octave"
                    or "--chord-octave" or "--guide-octave" or "--double-octave":
                    if (!TryTakeValue(args, ref i, text, out var octaveText, out error))
                    {
                        return false;
                    }

                    var maxOctaves = arg switch
                    {
                        "--bass-octave" => ConversionOptionsValidator.MaxBassOctaveShift,
                        "--chord-octave" => ConversionOptionsValidator.MaxChordOctaveShift,
                        "--guide-octave" => ConversionOptionsValidator.MaxGuideToneOctaveShift,
                        "--double-octave" => ConversionOptionsValidator.MaxDoublingSemitones / 12,
                        _ => ConversionOptionsValidator.MaxOctaveShift,
                    };
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
                        "--chord-octave" => result with { ChordOctave = octaves },
                        "--guide-octave" => result with { GuideOctave = octaves, GuideTones = true },
                        "--double-octave" => result with { DoubleOctave = octaves, DoubleVocal = true },
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
                case "--chord-voicing":
                    if (!TryTakeValue(args, ref i, text, out var voicingText, out error))
                    {
                        return false;
                    }

                    if (!ChordInversions.TryGetValue(voicingText, out var inversion))
                    {
                        error = text.Format(text.InvalidChordVoicing, voicingText, string.Join(", ", ChordInversions.Keys));
                        return false;
                    }

                    result = result with { Inversion = inversion };
                    break;
                case "--channel" or "--program":
                    if (!TryTakeValue(args, ref i, text, out var assignment, out error))
                    {
                        return false;
                    }

                    var separator = assignment.LastIndexOf('=');
                    var isChannel = arg == "--channel";
                    var limit = isChannel ? 16 : 128;
                    if (separator <= 0
                        || !int.TryParse(assignment[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                        || value < 1
                        || value > limit)
                    {
                        error = text.Format(text.InvalidAssignment, arg, assignment, limit);
                        return false;
                    }

                    (isChannel ? result.MidiChannels : result.MidiPrograms)[assignment[..separator]] = value;
                    break;
                case "--no-chords":
                    result = result with { IncludeChords = false };
                    break;
                case "--split-drums":
                    result = result with { SplitDrums = true, Drums = result.Drums ?? DrumPattern.FourOnTheFloor };
                    break;
                case "--drum-note":
                    if (!TryTakeValue(args, ref i, text, out var drumNote, out error))
                    {
                        return false;
                    }

                    var equals = drumNote.IndexOf('=');
                    if (equals <= 0
                        || !DrumNames.TryGetValue(drumNote[..equals].Trim(), out var drum)
                        || !NoteNames.TryParse(drumNote[(equals + 1)..], out var note))
                    {
                        error = text.Format(text.InvalidDrumNote, drumNote, string.Join(", ", DrumNames.Keys));
                        return false;
                    }

                    var notes = result.DrumNotes ?? new DrumNotes();
                    result = result with { DrumNotes = WithNote(notes, drum, note), Drums = result.Drums ?? DrumPattern.FourOnTheFloor };
                    break;
                case "--no-crash":
                    result = result with { Crash = false };
                    break;
                case "--guide-tones":
                    result = result with { GuideTones = true };
                    break;
                case "--double-vocal":
                    result = result with { DoubleVocal = true };
                    break;
                case "--swing":
                    if (!TryTakePercent(args, ref i, text, out var swing, out error))
                    {
                        return false;
                    }

                    result = result with { SwingPercent = swing };
                    break;
                case "--swing-unit":
                    if (!TryTakeValue(args, ref i, text, out var unitText, out error))
                    {
                        return false;
                    }

                    if (!SwingUnits.TryGetValue(unitText, out var unit))
                    {
                        error = text.Format(text.InvalidSwingUnit, unitText, string.Join(", ", SwingUnits.Keys));
                        return false;
                    }

                    result = result with { SwingUnit = unit };
                    break;
                case "--straight-drums":
                    result = result with { StraightDrums = true };
                    break;
                case "--humanize":
                    if (!TryTakePercent(args, ref i, text, out var humanize, out error))
                    {
                        return false;
                    }

                    result = result with { HumanizePercent = humanize };
                    break;
                case "--mono":
                    result = result with { Mono = true };
                    break;
                case "--legato":
                    result = result with { Mono = true, Legato = true };
                    break;
                case "--logic-split-sections":
                    result = result with { SplitSections = true };
                    break;
                case "--count-in":
                    if (!TryTakeValue(args, ref i, text, out var barsText, out error))
                    {
                        return false;
                    }

                    if (!int.TryParse(barsText, NumberStyles.None, CultureInfo.InvariantCulture, out var bars)
                        || bars > ConversionOptionsValidator.MaxCountInBars)
                    {
                        error = text.Format(text.InvalidCountIn, barsText, ConversionOptionsValidator.MaxCountInBars);
                        return false;
                    }

                    result = result with { CountIn = bars };
                    break;
                case "--count-in-silent":
                    result = result with { CountInClick = false, CountIn = result.CountIn == 0 ? 1 : result.CountIn };
                    break;
                case "--fit-tempo":
                    result = result with { FitTempo = true };
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

        if (result.FitTempo && result.LogicAudioPath is null)
        {
            error = text.FitTempoNeedsAudio;
            return false;
        }

        result = result with { InputPath = input };
        return true;
    }

    /// <summary>MIDI channel per track name, from <c>--channel Bass=5</c>.</summary>
    public Dictionary<string, int> MidiChannels { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Program change per track name, from <c>--program Bass=34</c>.</summary>
    public Dictionary<string, int> MidiPrograms { get; init; } = new(StringComparer.OrdinalIgnoreCase);

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

        // A voicing or a register alone is reason enough for a chord track; it then plays the chords as written.
        var chords = Chords is not null || Inversion is not null || ChordOctave != 0
            ? new ChordOptions
            {
                Pattern = Chords ?? ChordPattern.Block,
                Inversion = Inversion ?? ChordInversion.RootPosition,
                OctaveShift = ChordOctave,
            }
            : null;

        return new ArrangementOptions
        {
            DefaultOctaveShift = Octave,
            OctaveShifts = shifts,
            Bass = Bass is { } pattern ? new BassOptions { Pattern = pattern, OctaveShift = BassOctave } : null,
            Drums = Drums is { } drums ? new DrumOptions { Pattern = drums, CrashOnSections = Crash, SeparateTracks = SplitDrums, Notes = DrumNotes } : null,
            Chords = chords,
            GuideTones = GuideTones ? new GuideToneOptions { OctaveShift = GuideOctave } : null,
            Doubling = DoubleVocal ? new DoublingOptions { Semitones = 12 * DoubleOctave } : null,
            Groove = SwingPercent > 0 || HumanizePercent > 0
                ? new GrooveOptions
                {
                    Swing = SwingPercent / 100.0,
                    SwingUnit = SwingUnit,
                    IncludeDrums = !StraightDrums,
                    HumanizeTimingMs = HumanizePercent / 100.0 * MaxHumanizeTimingMs,
                    HumanizeVelocity = (int)Math.Round(HumanizePercent / 100.0 * MaxHumanizeVelocity),
                }
                : null,
            Mono = Mono ? new MonoOptions { Legato = Legato } : null,
            CountIn = CountIn > 0 ? new CountInOptions { Bars = CountIn, Click = CountInClick } : null,
        };
    }

    /// <summary>What <c>--humanize 100</c> means: a note moves by up to this much.</summary>
    private const double MaxHumanizeTimingMs = 25;

    /// <summary>What <c>--humanize 100</c> means for the velocities.</summary>
    private const int MaxHumanizeVelocity = 24;

    private static readonly Dictionary<string, BassPattern> BassPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eighths"] = BassPattern.Eighths,
        ["quarters"] = BassPattern.Quarters,
        ["root-fifth"] = BassPattern.RootFifth,
        ["octaves"] = BassPattern.Octaves,
        ["offbeat"] = BassPattern.Offbeat,
        ["sustained"] = BassPattern.Sustained,
        ["walking"] = BassPattern.Walking,
    };

    private static readonly Dictionary<string, ChordPattern> ChordPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["block"] = ChordPattern.Block,
        ["eighths"] = ChordPattern.Eighths,
        ["offbeat"] = ChordPattern.Offbeat,
        ["arpeggio"] = ChordPattern.ArpeggioUp,
        ["arpeggio-up-down"] = ChordPattern.ArpeggioUpDown,
        ["sixteenths"] = ChordPattern.Sixteenths,
    };

    private static readonly Dictionary<string, ChordInversion> ChordInversions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["root"] = ChordInversion.RootPosition,
        ["closest"] = ChordInversion.Closest,
        ["first"] = ChordInversion.First,
        ["second"] = ChordInversion.Second,
    };

    private static readonly Dictionary<string, SwingUnit> SwingUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eighths"] = SwingUnit.Eighths,
        ["sixteenths"] = SwingUnit.Sixteenths,
    };

    /// <summary>The drums of <c>--drum-note</c>, spelled as the flag takes them.</summary>
    private static readonly Dictionary<string, Drum> DrumNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kick"] = Drum.Kick,
        ["snare"] = Drum.Snare,
        ["closed-hihat"] = Drum.ClosedHiHat,
        ["open-hihat"] = Drum.OpenHiHat,
        ["crash"] = Drum.Crash,
        ["clap"] = Drum.Clap,
    };

    private static DrumNotes WithNote(DrumNotes notes, Drum drum, int note) => drum switch
    {
        Drum.Kick => notes with { Kick = note },
        Drum.Snare => notes with { Snare = note },
        Drum.ClosedHiHat => notes with { ClosedHiHat = note },
        Drum.OpenHiHat => notes with { OpenHiHat = note },
        Drum.Crash => notes with { Crash = note },
        Drum.Clap => notes with { Clap = note },
        _ => notes,
    };

    private static readonly Dictionary<string, DrumPattern> DrumPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["four-on-the-floor"] = DrumPattern.FourOnTheFloor,
        ["backbeat"] = DrumPattern.Backbeat,
        ["half-time"] = DrumPattern.HalfTime,
        ["disco"] = DrumPattern.Disco,
        ["sixteenth-hats"] = DrumPattern.SixteenthHats,
        ["shuffle"] = DrumPattern.Shuffle,
    };

    private static bool TryTakePercent(string[] args, ref int i, CliText text, out int value, out string? error)
    {
        value = 0;
        if (!TryTakeValue(args, ref i, text, out var percentText, out error))
        {
            return false;
        }

        var option = args[i - 1];
        if (!int.TryParse(percentText, NumberStyles.None, CultureInfo.InvariantCulture, out value) || value > 100)
        {
            error = text.Format(text.InvalidPercent, option, percentText);
            return false;
        }

        return true;
    }

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
