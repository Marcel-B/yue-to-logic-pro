using System.Globalization;
using YueToLogic.Core.Diagnostics;

namespace YueToLogic.Cli;

/// <summary>
/// User-facing CLI strings in English and German, chosen by the UI culture. Diagnostic messages from
/// the core library stay English; their codes are stable if they ever need translating.
/// </summary>
internal sealed class CliText
{
    public required string Usage { get; init; }
    public required string MissingInput { get; init; }
    public required string UnknownOption { get; init; }
    public required string UnexpectedArgument { get; init; }
    public required string MissingValue { get; init; }
    public required string InvalidPpq { get; init; }
    public required string InvalidOctave { get; init; }
    public required string InvalidBassPattern { get; init; }
    public required string InvalidDrumPattern { get; init; }
    public required string InvalidDrumNote { get; init; }
    public required string InvalidChordPattern { get; init; }
    public required string InvalidAssignment { get; init; }
    public required string InvalidChordVoicing { get; init; }
    public required string InvalidSwingUnit { get; init; }
    public required string InvalidPercent { get; init; }
    public required string InvalidCountIn { get; init; }
    public required string InputNotFound { get; init; }
    public required string OutputExists { get; init; }
    public required string ReadFailed { get; init; }
    public required string WriteFailed { get; init; }
    public required string ConversionFailed { get; init; }
    public required string Error { get; init; }
    public required string Warning { get; init; }
    public required string Info { get; init; }
    public required string LineColumn { get; init; }
    public required string LineOnly { get; init; }
    public required string HiddenInfos { get; init; }
    public required string LabelInput { get; init; }
    public required string LabelTempo { get; init; }
    public required string LabelMeter { get; init; }
    public required string LabelKey { get; init; }
    public required string LabelLength { get; init; }
    public required string LabelSections { get; init; }
    public required string LabelTracks { get; init; }
    public required string LabelMidi { get; init; }
    public required string LabelJson { get; init; }
    public required string LabelLogic { get; init; }
    public required string AudioNotFound { get; init; }
    public required string LogicFailed { get; init; }
    public required string LengthValue { get; init; }
    public required string SectionValue { get; init; }
    public required string NotesValue { get; init; }
    public required string ChordsValue { get; init; }
    public required string ChordsSkipped { get; init; }
    public required string None { get; init; }
    public required string InvalidTrackRole { get; init; }
    public required string InvalidSkipBars { get; init; }
    public required string NotForMidiInput { get; init; }
    public required string OnlyForMidiInput { get; init; }
    public required string MidiConversionFailed { get; init; }
    public required string LabelAbc { get; init; }
    public required string TrackValue { get; init; }
    public required string RoleIgnored { get; init; }

    public static CliText ForCurrentCulture() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? German : English;

    public string Format(string format, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, format, args);

    public string Severity(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Error => Error,
        DiagnosticSeverity.Warning => Warning,
        _ => Info,
    };

    private static readonly CliText English = new()
    {
        Usage = """
            yue2logic - converts a YuE2 score.abc into a MIDI file for Logic Pro, and a MIDI file back into a score.abc

            Usage:
              yue2logic <score.abc> [options]
              yue2logic <song.mid> [-o <score.abc>] [--track <n>=<role>] [--skip-bars <n>]

            Options:
              -o, --output <file>     MIDI file to write, .mid is added if missing (default: <input>.mid)
                  --no-chords         Do not write the chord track
                  --octave <n>        Move both voices by n octaves (-4 to 4, e.g. -1)
                  --vocal-octave <n>  Move only the vocal melody (takes precedence over --octave)
                  --ins-octave <n>    Move only the instrumental melody (takes precedence over --octave)
                  --bass              Add a bass track playing the chord roots in eighth notes
                  --bass-pattern <p>  Bass rhythm: eighths (default), quarters, root-fifth, octaves, offbeat,
                                      sustained, walking; implies --bass
                  --bass-octave <n>   Move the bass by n octaves (-2 to 2); implies --bass
                  --drums             Add a four-on-the-floor drum track with a crash on every section
                  --drum-pattern <p>  Drum groove: four-on-the-floor (default), backbeat (kick 1+3, snare 2+4,
                                      open hi-hat on 4+), half-time, disco, sixteenth-hats, shuffle;
                                      implies --drums
                  --split-drums       One track per drum: Kick, Snare, HiHat, Crash; implies --drums
                  --drum-note <d>=<n> Note a drum machine plays drum d on: kick, snare, closed-hihat,
                                      open-hihat, crash, clap; n a number (36) or a name as Logic shows it
                                      (C1); repeatable, the rest stay General MIDI; implies --drums
                  --chord-pattern <p> How the chords are played: block (default, as written), eighths,
                                      sixteenths, offbeat, arpeggio, arpeggio-up-down
                  --chord-voicing <v> Inversion of the chords: root (default), closest (smooth voice leading),
                                      first, second
                  --chord-octave <n>  Move the chord track by n octaves (-2 to 2)
                  --no-crash          No crash cymbal at the start of a section
                  --guide-tones       Add a held track of each chord's third and seventh (a pad)
                  --guide-octave <n>  Move the guide tones by n octaves (-2 to 2); implies --guide-tones
                  --double-vocal      Double the vocal melody an octave below, on a track of its own
                  --double-octave <n> Octave of that copy (-2 to 2, default -1); implies --double-vocal
                  --swing <n>         Swing in percent: 0 straight (default), 100 a full triplet feel
                  --swing-unit <u>    Which subdivision swings: eighths (default), sixteenths
                  --straight-drums    Keep the drums on the grid while everything else swings
                  --humanize <n>      Vary timing and velocity by n percent (0 default, 100 = 25 ms)
                  --mono              Prepare the melodies for monophonic synthesizers: one note at a time,
                                      a gap before the next one, no note too short to sound
                  --legato            As --mono, but every note also reaches to the next one
                  --logic-split-sections
                                      One region per song section in the Logic project, named after it
                  --count-in <n>      Silent bars in front of the song (0 to 8), with a click on every beat
                  --count-in-silent   No click in those bars; implies --count-in 1
                  --channel <t>=<n>   MIDI channel 1-16 for track t (Vocal, Ins, Chords, Bass, Drums, Guide,
                                      "Vocal 8vb"); repeatable, others get the next free channel
                  --program <t>=<n>   Program change 1-128 sent at the start of track t; repeatable
                  --ppq <n>           MIDI resolution in ticks per quarter note (default: 480)
                  --dump-json <file>  Also write the parsed score and diagnostics as JSON (.json is added if missing)
                  --logic <audio.flac> Also write a Logic Pro project (<output>.logicx) with the MIDI tracks and this audio
                  --logic-no-audio    Also write a Logic Pro project without audio (its audio track stays empty)
                  --vocals <file>     Separated vocals (WAV) for the project's second audio track
                  --vocals-dry <file> Separated vocals without reverb (WAV) for its third audio track
              -f, --force             Overwrite existing output files
              -v, --verbose           Also show informational messages
              -h, --help              Show this help

            The way back: a MIDI file (.mid, e.g. exported from Logic after editing the project) becomes a
            score.abc for YuE2 again (default: <input>.abc). The tracks named Vocal, Ins and Chords become
            the two voices and the chord symbols, notes move to the sixteenth grid, and silent bars at the
            start (a count-in) are left out.
                  --track <n>=<role>  Role of the n-th track as the summary lists it: vocal, ins, chords or
                                      ignore; repeatable, the rest keep the role their name suggests
                  --skip-bars <n>     Leave out the first n bars (default: the silent ones)

            Exit codes: 0 success, 1 score could not be converted, 2 invalid arguments or file error
            """,
        MissingInput = "No input file given.",
        UnknownOption = "Unknown option '{0}'.",
        UnexpectedArgument = "Unexpected argument '{0}'; only one input file is supported.",
        MissingValue = "Option '{0}' needs a value.",
        InvalidPpq = "Invalid --ppq value '{0}'; expected a number between 24 and 32767.",
        InvalidOctave = "Invalid {0} value '{1}'; expected a whole number between -{2} and {2}.",
        InvalidBassPattern = "Unknown bass pattern '{0}'; expected one of: {1}.",
        InvalidDrumPattern = "Unknown drum pattern '{0}'; expected one of: {1}.",
        InvalidDrumNote = "--drum-note expects <drum>=<note> with a drum of {1} and a note from 0 to 127 or a name such as C1, got '{0}'.",
        InvalidChordPattern = "Unknown chord pattern '{0}'; expected one of: {1}.",
        InvalidAssignment = "{0} expects <track>=<number> with a number from 1 to {2}, got '{1}'.",
        InvalidChordVoicing = "Unknown chord voicing '{0}'; expected one of: {1}.",
        InvalidSwingUnit = "Unknown swing unit '{0}'; expected one of: {1}.",
        InvalidPercent = "Invalid {0} value '{1}'; expected a whole number between 0 and 100.",
        InvalidCountIn = "Invalid --count-in value '{0}'; expected a whole number between 0 and {1}.",
        InputNotFound = "Input file not found: {0}",
        OutputExists = "Output file already exists: {0} (use --force to overwrite)",
        ReadFailed = "Could not read {0}: {1}",
        WriteFailed = "Could not write {0}: {1}",
        ConversionFailed = "The score could not be converted.",
        Error = "error",
        Warning = "warning",
        Info = "info",
        LineColumn = "line {0}, column {1}",
        LineOnly = "line {0}",
        HiddenInfos = "{0} informational message(s) hidden; use --verbose to show them.",
        LabelInput = "Input",
        LabelTempo = "Tempo",
        LabelMeter = "Meter",
        LabelKey = "Key",
        LabelLength = "Length",
        LabelSections = "Sections",
        LabelTracks = "Tracks",
        LabelMidi = "MIDI",
        LabelJson = "JSON",
        LabelLogic = "Logic",
        AudioNotFound = "Audio file not found: {0}",
        LogicFailed = "The Logic project could not be written.",
        LengthValue = "{0} bars, {1:0.0} s",
        SectionValue = "{0} (bar {1})",
        NotesValue = "{0}: {1} notes",
        ChordsValue = "Chords: {0}",
        ChordsSkipped = "Chords: not written (--no-chords)",
        None = "none",
        InvalidTrackRole = "--track expects <n>=<role> with the number of a track from 1 and a role of {1}, got '{0}'.",
        InvalidSkipBars = "Invalid --skip-bars value '{0}'; expected a whole number between 0 and {1}.",
        NotForMidiInput = "{0} does not apply to a MIDI file as input; that only becomes a score.abc.",
        OnlyForMidiInput = "{0} only applies to a MIDI file (.mid) as input.",
        MidiConversionFailed = "The MIDI file could not be turned into a score.",
        LabelAbc = "ABC",
        TrackValue = "{0} {1} → {2}",
        RoleIgnored = "ignored",
    };

    private static readonly CliText German = new()
    {
        Usage = """
            yue2logic - wandelt eine YuE2-score.abc in eine MIDI-Datei für Logic Pro um und eine MIDI-Datei zurück in eine score.abc

            Aufruf:
              yue2logic <score.abc> [Optionen]
              yue2logic <song.mid> [-o <score.abc>] [--track <n>=<rolle>] [--skip-bars <n>]

            Optionen:
              -o, --output <datei>    Zu schreibende MIDI-Datei, .mid wird ggf. ergänzt (Standard: <eingabe>.mid)
                  --no-chords         Keine Akkordspur schreiben
                  --octave <n>        Beide Stimmen um n Oktaven verschieben (-4 bis 4, z. B. -1)
                  --vocal-octave <n>  Nur die Gesangsmelodie verschieben (hat Vorrang vor --octave)
                  --ins-octave <n>    Nur die Instrumentalmelodie verschieben (hat Vorrang vor --octave)
                  --bass              Bassspur hinzufügen, spielt die Akkordgrundtöne in Achteln
                  --bass-pattern <p>  Bassrhythmus: eighths (Standard), quarters, root-fifth, octaves, offbeat,
                                      sustained, walking; schließt --bass ein
                  --bass-octave <n>   Bass um n Oktaven verschieben (-2 bis 2); schließt --bass ein
                  --drums             Schlagzeugspur (Four on the Floor) mit Crash zu jedem Abschnitt hinzufügen
                  --drum-pattern <p>  Groove: four-on-the-floor (Standard), backbeat (Kick 1+3, Snare 2+4,
                                      offene Hi-Hat auf 4+), half-time, disco, sixteenth-hats, shuffle;
                                      schließt --drums ein
                  --split-drums       Eine Spur je Trommel: Kick, Snare, HiHat, Crash; schließt --drums ein
                  --drum-note <t>=<n> Note, auf der ein Drumcomputer die Trommel t spielt: kick, snare,
                                      closed-hihat, open-hihat, crash, clap; n als Zahl (36) oder Name wie in
                                      Logic (C1); mehrfach möglich, der Rest bleibt General MIDI; schließt
                                      --drums ein
                  --chord-pattern <p> Akkordbegleitung: block (Standard, wie notiert), eighths, sixteenths,
                                      offbeat, arpeggio, arpeggio-up-down
                  --chord-voicing <v> Akkordlage: root (Standard), closest (weiche Stimmführung), first, second
                  --chord-octave <n>  Akkordspur um n Oktaven verschieben (-2 bis 2)
                  --no-crash          Kein Crash-Becken zu Beginn eines Abschnitts
                  --guide-tones       Liegende Spur aus Terz und Septime jedes Akkords hinzufügen (Fläche)
                  --guide-octave <n>  Diese Spur um n Oktaven verschieben (-2 bis 2); schließt --guide-tones ein
                  --double-vocal      Gesangsmelodie eine Oktave tiefer auf einer eigenen Spur verdoppeln
                  --double-octave <n> Oktave dieser Kopie (-2 bis 2, Standard -1); schließt --double-vocal ein
                  --swing <n>         Swing in Prozent: 0 gerade (Standard), 100 volles Triolenfeeling
                  --swing-unit <e>    Welche Unterteilung swingt: eighths (Standard), sixteenths
                  --straight-drums    Schlagzeug gerade lassen, während alles andere swingt
                  --humanize <n>      Timing und Anschlag um n Prozent streuen (0 Standard, 100 = 25 ms)
                  --mono              Melodien für monophone Synthesizer aufbereiten: immer nur ein Ton,
                                      Lücke vor dem nächsten, keine zu kurzen Noten
                  --legato            Wie --mono, zusätzlich reicht jede Note bis zur nächsten
                  --logic-split-sections
                                      Im Logic-Projekt eine Region pro Songabschnitt, nach ihm benannt
                  --count-in <n>      Stille Takte vor dem Song (0 bis 8), mit Klick auf jedem Schlag
                  --count-in-silent   Kein Klick in diesen Takten; schließt --count-in 1 ein
                  --channel <s>=<n>   MIDI-Kanal 1-16 für Spur s (Vocal, Ins, Chords, Bass, Drums, Guide,
                                      "Vocal 8vb"); mehrfach möglich, der Rest bekommt den nächsten freien
                  --program <s>=<n>   Programmwechsel 1-128 zu Beginn der Spur s; mehrfach möglich
                  --ppq <n>           MIDI-Auflösung in Ticks pro Viertelnote (Standard: 480)
                  --dump-json <datei> Zusätzlich Score und Meldungen als JSON schreiben, .json wird ggf. ergänzt
                  --logic <audio.flac> Zusätzlich ein Logic-Pro-Projekt (<ausgabe>.logicx) mit den MIDI-Spuren und diesem Audio
                  --logic-no-audio    Zusätzlich ein Logic-Pro-Projekt ohne Audio (die Audiospur bleibt leer)
                  --vocals <datei>    Getrennter Gesang (WAV) für die zweite Audiospur des Projekts
                  --vocals-dry <datei> Getrennter Gesang ohne Hall (WAV) für die dritte Audiospur
              -f, --force             Vorhandene Ausgabedateien überschreiben
              -v, --verbose           Auch Info-Meldungen anzeigen
              -h, --help              Diese Hilfe anzeigen

            Der Rückweg: Aus einer MIDI-Datei (.mid, z. B. nach dem Bearbeiten aus Logic exportiert) wird
            wieder eine score.abc für YuE2 (Standard: <eingabe>.abc). Die Spuren Vocal, Ins und Chords werden
            zu den beiden Stimmen und den Akkordsymbolen, Noten rücken auf das Sechzehntelraster, und stille
            Takte am Anfang (ein Einzähler) fallen weg.
                  --track <n>=<rolle> Rolle der n-ten Spur, wie die Zusammenfassung sie auflistet: vocal, ins,
                                      chords oder ignore; mehrfach möglich, der Rest behält die Rolle, die
                                      sein Name nahelegt
                  --skip-bars <n>     Die ersten n Takte weglassen (Standard: die stillen)

            Exit-Codes: 0 Erfolg, 1 Score nicht konvertierbar, 2 ungültige Argumente oder Dateifehler
            """,
        MissingInput = "Keine Eingabedatei angegeben.",
        UnknownOption = "Unbekannte Option '{0}'.",
        UnexpectedArgument = "Unerwartetes Argument '{0}'; es wird nur eine Eingabedatei unterstützt.",
        MissingValue = "Option '{0}' benötigt einen Wert.",
        InvalidPpq = "Ungültiger Wert für --ppq: '{0}'; erwartet wird eine Zahl zwischen 24 und 32767.",
        InvalidOctave = "Ungültiger Wert für {0}: '{1}'; erwartet wird eine ganze Zahl zwischen -{2} und {2}.",
        InvalidBassPattern = "Unbekanntes Bassmuster '{0}'; erlaubt sind: {1}.",
        InvalidDrumPattern = "Unbekanntes Schlagzeugmuster '{0}'; erlaubt sind: {1}.",
        InvalidDrumNote = "--drum-note erwartet <Trommel>=<Note> mit einer Trommel aus {1} und einer Note von 0 bis 127 oder einem Namen wie C1, bekam '{0}'.",
        InvalidChordPattern = "Unbekanntes Akkordmuster '{0}'; erlaubt sind: {1}.",
        InvalidAssignment = "{0} erwartet <Spur>=<Zahl> mit einer Zahl von 1 bis {2}, bekam '{1}'.",
        InvalidChordVoicing = "Unbekannte Akkordlage '{0}'; erlaubt sind: {1}.",
        InvalidSwingUnit = "Unbekannte Swing-Einheit '{0}'; erlaubt sind: {1}.",
        InvalidPercent = "Ungültiger Wert für {0}: '{1}'; erwartet wird eine ganze Zahl zwischen 0 und 100.",
        InvalidCountIn = "Ungültiger Wert für --count-in: '{0}'; erwartet wird eine ganze Zahl zwischen 0 und {1}.",
        InputNotFound = "Eingabedatei nicht gefunden: {0}",
        OutputExists = "Ausgabedatei existiert bereits: {0} (mit --force überschreiben)",
        ReadFailed = "{0} konnte nicht gelesen werden: {1}",
        WriteFailed = "{0} konnte nicht geschrieben werden: {1}",
        ConversionFailed = "Der Score konnte nicht konvertiert werden.",
        Error = "Fehler",
        Warning = "Warnung",
        Info = "Info",
        LineColumn = "Zeile {0}, Spalte {1}",
        LineOnly = "Zeile {0}",
        HiddenInfos = "{0} Info-Meldung(en) ausgeblendet; mit --verbose anzeigen.",
        LabelInput = "Eingabe",
        LabelTempo = "Tempo",
        LabelMeter = "Taktart",
        LabelKey = "Tonart",
        LabelLength = "Länge",
        LabelSections = "Abschnitte",
        LabelTracks = "Spuren",
        LabelMidi = "MIDI",
        LabelJson = "JSON",
        LabelLogic = "Logic",
        AudioNotFound = "Audiodatei nicht gefunden: {0}",
        LogicFailed = "Das Logic-Projekt konnte nicht geschrieben werden.",
        LengthValue = "{0} Takte, {1:0.0} s",
        SectionValue = "{0} (Takt {1})",
        NotesValue = "{0}: {1} Noten",
        ChordsValue = "Akkorde: {0}",
        ChordsSkipped = "Akkorde: nicht geschrieben (--no-chords)",
        None = "keine",
        InvalidTrackRole = "--track erwartet <n>=<Rolle> mit der Nummer einer Spur ab 1 und einer Rolle aus {1}, bekam '{0}'.",
        InvalidSkipBars = "Ungültiger Wert für --skip-bars: '{0}'; erwartet wird eine ganze Zahl zwischen 0 und {1}.",
        NotForMidiInput = "{0} gilt nicht für eine MIDI-Datei als Eingabe; aus der wird nur eine score.abc.",
        OnlyForMidiInput = "{0} gilt nur für eine MIDI-Datei (.mid) als Eingabe.",
        MidiConversionFailed = "Aus der MIDI-Datei konnte kein Score werden.",
        LabelAbc = "ABC",
        TrackValue = "{0} {1} → {2}",
        RoleIgnored = "ignoriert",
    };
}
