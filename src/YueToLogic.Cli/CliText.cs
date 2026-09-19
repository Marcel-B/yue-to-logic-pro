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
            yue2logic - converts a YuE2 score.abc into a MIDI file for Logic Pro

            Usage:
              yue2logic <score.abc> [options]

            Options:
              -o, --output <file>     MIDI file to write, .mid is added if missing (default: <input>.mid)
                  --no-chords         Do not write the chord track
                  --octave <n>        Move both voices by n octaves (-4 to 4, e.g. -1)
                  --vocal-octave <n>  Move only the vocal melody (takes precedence over --octave)
                  --ins-octave <n>    Move only the instrumental melody (takes precedence over --octave)
                  --bass              Add a bass track playing the chord roots in eighth notes
                  --bass-pattern <p>  Bass rhythm: eighths (default), quarters, root-fifth; implies --bass
                  --bass-octave <n>   Move the bass by n octaves (-2 to 2); implies --bass
                  --drums             Add a four-on-the-floor drum track with a crash on every section
                  --drum-pattern <p>  Drum groove: four-on-the-floor (default), backbeat (kick 1+3, snare 2+4,
                                      open hi-hat on 4+); implies --drums
                  --ppq <n>           MIDI resolution in ticks per quarter note (default: 480)
                  --dump-json <file>  Also write the parsed score and diagnostics as JSON (.json is added if missing)
                  --logic <audio.flac> Also write a Logic Pro project (<output>.logicx) with the MIDI tracks and this audio
              -f, --force             Overwrite existing output files
              -v, --verbose           Also show informational messages
              -h, --help              Show this help

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
    };

    private static readonly CliText German = new()
    {
        Usage = """
            yue2logic - wandelt eine YuE2-score.abc in eine MIDI-Datei für Logic Pro um

            Aufruf:
              yue2logic <score.abc> [Optionen]

            Optionen:
              -o, --output <datei>    Zu schreibende MIDI-Datei, .mid wird ggf. ergänzt (Standard: <eingabe>.mid)
                  --no-chords         Keine Akkordspur schreiben
                  --octave <n>        Beide Stimmen um n Oktaven verschieben (-4 bis 4, z. B. -1)
                  --vocal-octave <n>  Nur die Gesangsmelodie verschieben (hat Vorrang vor --octave)
                  --ins-octave <n>    Nur die Instrumentalmelodie verschieben (hat Vorrang vor --octave)
                  --bass              Bassspur hinzufügen, spielt die Akkordgrundtöne in Achteln
                  --bass-pattern <p>  Bassrhythmus: eighths (Standard), quarters, root-fifth; schließt --bass ein
                  --bass-octave <n>   Bass um n Oktaven verschieben (-2 bis 2); schließt --bass ein
                  --drums             Schlagzeugspur (Four on the Floor) mit Crash zu jedem Abschnitt hinzufügen
                  --drum-pattern <p>  Groove: four-on-the-floor (Standard), backbeat (Kick 1+3, Snare 2+4,
                                      offene Hi-Hat auf 4+); schließt --drums ein
                  --ppq <n>           MIDI-Auflösung in Ticks pro Viertelnote (Standard: 480)
                  --dump-json <datei> Zusätzlich Score und Meldungen als JSON schreiben, .json wird ggf. ergänzt
                  --logic <audio.flac> Zusätzlich ein Logic-Pro-Projekt (<ausgabe>.logicx) mit den MIDI-Spuren und diesem Audio
              -f, --force             Vorhandene Ausgabedateien überschreiben
              -v, --verbose           Auch Info-Meldungen anzeigen
              -h, --help              Diese Hilfe anzeigen

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
    };
}
