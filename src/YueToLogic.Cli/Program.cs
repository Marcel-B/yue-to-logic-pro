using System.Text.Json;
using YueToLogic.Cli;
using YueToLogic.Core.Conversion;
using YueToLogic.Core.Diagnostics;
using YueToLogic.Core.Logic;
using YueToLogic.Core.Model;
using YueToLogic.Core.Serialization;

const int ExitSuccess = 0;
const int ExitConversionFailed = 1;
const int ExitUsageOrIoError = 2;

var text = CliText.ForCurrentCulture();

if (!CliArguments.TryParse(args, text, out var options, out var argumentError))
{
    Console.Error.WriteLine(argumentError);
    Console.Error.WriteLine();
    Console.Error.WriteLine(text.Usage);
    return ExitUsageOrIoError;
}

if (options.ShowHelp)
{
    Console.WriteLine(text.Usage);
    return ExitSuccess;
}

var inputPath = Path.GetFullPath(options.InputPath);
var midiPath = Path.GetFullPath(options.OutputPath is null
    ? Path.ChangeExtension(inputPath, ".mid")
    : EnsureExtension(options.OutputPath, ".mid", ".midi"));
var jsonPath = options.JsonPath is null ? null : Path.GetFullPath(EnsureExtension(options.JsonPath, ".json"));
var logicAudioPath = options.LogicAudioPath is null ? null : Path.GetFullPath(options.LogicAudioPath);
var logicPath = options.WriteLogicProject ? Path.ChangeExtension(midiPath, ".logicx") : null;

if (!File.Exists(inputPath))
{
    Console.Error.WriteLine(text.Format(text.InputNotFound, inputPath));
    return ExitUsageOrIoError;
}

if (logicAudioPath is not null && !File.Exists(logicAudioPath))
{
    Console.Error.WriteLine(text.Format(text.AudioNotFound, logicAudioPath));
    return ExitUsageOrIoError;
}

foreach (var path in new[] { midiPath, jsonPath, logicPath })
{
    if (path is not null && (File.Exists(path) || Directory.Exists(path)) && !options.Force)
    {
        Console.Error.WriteLine(text.Format(text.OutputExists, path));
        return ExitUsageOrIoError;
    }
}

string abc;
try
{
    abc = await File.ReadAllTextAsync(inputPath);
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(text.Format(text.ReadFailed, inputPath, ex.Message));
    return ExitUsageOrIoError;
}

// The library does the arithmetic but never touches a file, so the recording is measured here.
TempoFitOptions? fitTempo = null;
if (options.FitTempo)
{
    var audio = await ReadAudioInfoAsync(logicAudioPath!);
    if (audio is null)
    {
        return ExitUsageOrIoError;
    }

    fitTempo = new TempoFitOptions { AudioSeconds = audio.DurationSeconds };
}

var result = new ScoreConverter().Convert(abc, new ConversionOptions
{
    TicksPerQuarterNote = options.TicksPerQuarterNote,
    IncludeChordTrack = options.IncludeChords,
    Arrangement = options.ToArrangementOptions(),
    FitTempo = fitTempo,
    MidiChannels = options.MidiChannels,
    MidiPrograms = options.MidiPrograms,
});

PrintDiagnostics(result.Diagnostics);

if (jsonPath is not null)
{
    // The MIDI bytes are written separately; leaving them out keeps the JSON readable.
    var json = JsonSerializer.Serialize(result with { Midi = null }, YueToLogicJsonContext.Default.ConversionResult);
    if (!await TryWriteAsync(jsonPath, () => File.WriteAllTextAsync(jsonPath, json)))
    {
        return ExitUsageOrIoError;
    }
}

if (!result.Success)
{
    Console.Error.WriteLine(text.ConversionFailed);
    return ExitConversionFailed;
}

if (!await TryWriteAsync(midiPath, () => File.WriteAllBytesAsync(midiPath, result.Midi!)))
{
    return ExitUsageOrIoError;
}

if (logicPath is not null && !await TryWriteLogicProjectAsync(result.Score!, logicAudioPath, logicPath, options.VocalsPath, options.VocalsDryPath))
{
    return ExitConversionFailed;
}

PrintSummary(result.Score!);
return ExitSuccess;

async Task<bool> TryWriteLogicProjectAsync(ScoreDocument score, string? audioPath, string packagePath, string? vocalsPath, string? vocalsDryPath)
{
    try
    {
        if (Directory.Exists(packagePath))
        {
            Directory.Delete(packagePath, recursive: true); // only reached with --force
        }

        await using var audio = audioPath is null ? null : File.OpenRead(audioPath);
        await using var vocals = vocalsPath is null ? null : File.OpenRead(vocalsPath);
        await using var vocalsDry = vocalsDryPath is null ? null : File.OpenRead(vocalsDryPath);
        var logic = await new LogicProjectWriter().WriteAsync(
            score,
            new LogicAudio(audio, vocals, vocalsDry),
            new DirectoryLogicPackageSink(packagePath),
            new LogicProjectOptions
            {
                ProjectName = Path.GetFileNameWithoutExtension(packagePath),
                SplitRegionsAtSections = options.SplitSections,
            });
        PrintDiagnostics(logic.Diagnostics);
        if (!logic.Success)
        {
            Console.Error.WriteLine(text.LogicFailed);
        }

        return logic.Success;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(text.Format(text.WriteFailed, packagePath, ex.Message));
        return false;
    }
}

/// <summary>Reads the STREAMINFO of a FLAC file, whose length the tempo fit is measured against.</summary>
async Task<FlacStreamInfo?> ReadAudioInfoAsync(string path)
{
    try
    {
        var header = new byte[FlacStreamInfo.HeaderLength];
        await using var stream = File.OpenRead(path);
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);
        if (FlacStreamInfo.TryParse(header.AsSpan(0, read), out var info))
        {
            return info;
        }

        Console.Error.WriteLine(text.Format(text.AudioUnreadable, path));
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(text.Format(text.ReadFailed, path, ex.Message));
    }

    return null;
}

async Task<bool> TryWriteAsync(string path, Func<Task> write)
{
    try
    {
        await write();
        return true;
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(text.Format(text.WriteFailed, path, ex.Message));
        return false;
    }
}

void PrintDiagnostics(IReadOnlyList<Diagnostic> diagnostics)
{
    var hidden = 0;
    foreach (var diagnostic in diagnostics)
    {
        if (diagnostic.Severity == DiagnosticSeverity.Info && !options.Verbose)
        {
            hidden++;
            continue;
        }

        var location = (diagnostic.Line, diagnostic.Column) switch
        {
            ({ } line, { } column) => $" ({text.Format(text.LineColumn, line, column)})",
            ({ } line, null) => $" ({text.Format(text.LineOnly, line)})",
            _ => string.Empty,
        };
        Console.Error.WriteLine($"{text.Severity(diagnostic.Severity)} {diagnostic.Code}{location}: {diagnostic.Message}");
    }

    if (hidden > 0)
    {
        Console.Error.WriteLine(text.Format(text.HiddenInfos, hidden));
    }
}

void PrintSummary(ScoreDocument score)
{
    var bars = score.GetBarPosition(score.LengthTicks).Bar - 1;
    var sections = score.Sections.Count == 0
        ? text.None
        : string.Join(", ", score.Sections.Select(s => text.Format(text.SectionValue, s.Name, score.GetBarPosition(s.StartTicks).Bar)));
    // Same order as the tracks in the MIDI file: melodies, chords, generated accompaniment.
    string NotesOf(VoiceTrack voice) => text.Format(text.NotesValue, voice.Id, voice.Notes.Count);
    var tracks = score.Voices.Where(v => v.Kind == TrackKind.Melody).Select(NotesOf).ToList();
    tracks.Add(options.IncludeChords ? text.Format(text.ChordsValue, score.Chords.Count) : text.ChordsSkipped);
    tracks.AddRange(score.Voices.Where(v => v.Kind != TrackKind.Melody).Select(NotesOf));

    WriteRow(text.LabelInput, inputPath);
    WriteRow(text.LabelTempo, text.Format("{0:0.##} BPM", score.TempoBpm));
    WriteRow(text.LabelMeter, string.Join(", ", score.TimeSignatures.Select(t => $"{t.Numerator}/{t.Denominator}")));
    WriteRow(text.LabelKey, string.Join(", ", score.KeySignatures.Select(k => k.Key)));
    WriteRow(text.LabelLength, text.Format(text.LengthValue, bars, score.DurationSeconds));
    WriteRow(text.LabelSections, sections);
    WriteRow(text.LabelTracks, string.Join(" | ", tracks));
    WriteRow(text.LabelMidi, midiPath);
    if (jsonPath is not null)
    {
        WriteRow(text.LabelJson, jsonPath);
    }

    if (logicPath is not null)
    {
        WriteRow(text.LabelLogic, logicPath);
    }
}

// Appends the first extension unless the path already ends in one of them (case-insensitive):
// "song" and "song.v2" become "song.mid" / "song.v2.mid", while "song.MIDI" is kept.
static string EnsureExtension(string path, params string[] extensions) =>
    extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase) ? path : path + extensions[0];

static void WriteRow(string label, string value) => Console.WriteLine($"{label + ":",-12} {value}");
