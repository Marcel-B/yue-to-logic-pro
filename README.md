# YuE to Logic

[![CI](https://github.com/Marcel-B/yue-to-logic-pro/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcel-B/yue-to-logic-pro/actions/workflows/ci.yml)

[Deutsche Version](README.de.md)

[YuE](https://github.com/multimodal-art-projection/YuE) generates music with AI. Besides the audio (`audio.flac`), a YuE2 run with chain-of-thought mode `full` or `melody` writes a symbolic version of the song: `score.abc`. That file contains the tempo, meter, key, song structure (verse, chorus, …), a vocal and an instrumental melody, and the chord progression.

The long-term goal of this project is a Logic Pro project with the generated audio as a region and matching MIDI tracks below it. It is meant as a starting point for analysing, editing or extending a song, not as a perfect transcription.

**Current state:** a prototype that converts `score.abc` into a MIDI file you can open in Logic Pro.

## Usage

Requires the .NET 10 SDK.

```sh
dotnet run --project src/YueToLogic.Cli -- path/to/score.abc
```

This writes `path/to/score.mid` next to the input and prints a summary:

```
Input:       /…/score.abc
Tempo:       88 BPM
Meter:       4/4
Key:         C
Length:      8 bars, 21.8 s
Sections:    verse (bar 1), chorus (bar 5)
Tracks:      Vocal: 56 notes | Ins: 0 notes | Chords: 8
MIDI:        /…/score.mid
```

| Option | Meaning |
|---|---|
| `-o, --output <file>` | MIDI file to write; `.mid` is appended unless the name ends in `.mid` or `.midi` (default: `<input>.mid`) |
| `--no-chords` | Do not write the chord track |
| `--octave <n>` | Move both melodies by `n` octaves (−4 to 4) |
| `--vocal-octave <n>`, `--ins-octave <n>` | Move only one melody; takes precedence over `--octave` |
| `--bass` | Add a bass track (see below) |
| `--bass-pattern <p>` | Bass rhythm: `eighths` (default), `quarters`, `root-fifth`; implies `--bass` |
| `--bass-octave <n>` | Move the bass by `n` octaves (−2 to 2); implies `--bass` |
| `--drums` | Add a drum track (see below) |
| `--ppq <n>` | MIDI resolution in ticks per quarter note (default 480) |
| `--dump-json <file>` | Also write the parsed score and all diagnostics as JSON; `.json` is appended if missing |
| `-f, --force` | Overwrite existing output files |
| `-v, --verbose` | Also show informational messages |

Exit codes: `0` success, `1` the score could not be converted, `2` invalid arguments or a file error. The CLI answers in German or English depending on the system language.

### In Logic Pro

Preferably open the MIDI file with *File → Open*: Logic then creates a new project that takes tempo, meter and markers from the file, starting at bar 1. When dragging the file into an existing project instead, drop it exactly at bar 1 and confirm importing the tempo; tempo and meter are placed relative to the drop position, so everything before it keeps the project tempo. Then drag `audio.flac` from the same YuE output folder onto a new audio track at bar 1. Because the MIDI file carries the tempo from the score, both line up.

## What the MIDI file contains

A Standard MIDI File, type 1:

| Track | Content |
|---|---|
| `Conductor` | Tempo, meter, key and a marker for every section (`verse`, `chorus`, …); Logic puts these into its global tracks |
| `Vocal` | The vocal melody |
| `Ins` | The instrumental melody |
| `Chords` | The chord symbols played as block chords (root in octave 3, slash bass below), plus the symbol as a text event |
| `Bass` | Only with `--bass`: the bass note of every chord in the register E2–D♯3 (MIDI 40–51, in Logic's naming E1–D♯2), which every bass instrument can play; `--bass-octave -1` goes down to the lowest bass-guitar string. Slash chords such as `C/E` play their bass note. Notes are slightly detached, on-beat notes a little louder. `root-fifth` alternates quarter notes between bass note and the chord's fifth |
| `Drums` | Only with `--drums`: kick on every beat, snare on 2 and 4, closed hi-hat in eighth notes (off-beats softer) and a crash cymbal at the start of every section. General MIDI note numbers on channel 10, which Logic's drum kits understand. In 3/4 the snare plays on beat 2; 6/8 is counted in dotted quarters |

The arrangement options are also available in the library (`ConversionOptions.Arrangement`), and the generated tracks appear in the JSON output with `"kind": "Bass"` or `"Drums"`.

## The input format

YuE2 writes a deliberately small subset of ABC notation. A generic ABC parser would misread it: most importantly, an accidental applies to its note letter **in every octave** until the bar line (after `^F`, `f` is sharp too). The parser in this project follows the YuE2 rules and reports everything outside them as a diagnostic instead of failing, so hand-edited scores still convert. The full rules are described in [YuE2's ABC editing reference](https://github.com/multimodal-art-projection/YuE/blob/main/skills/yue2-music/references/abc-editing.md).

## Project layout

```
src/YueToLogic.Core/    Library: ABC parsing, score model, MIDI rendering
src/YueToLogic.Cli/     Command-line tool (yue2logic)
tests/                  xUnit tests
samples/score.abc       Official YuE2 example score
```

`YueToLogic.Core` has no console or file-system dependencies so that it can later be used from a web service, an Electron/Vue frontend or a macOS app:

- Input is a `string` or `Stream`, output is a `byte[]` or a caller-supplied `Stream`.
- `services.AddYueToLogic()` registers the stateless `IScoreConverter`, `IAbcScoreParser`, `IScoreArranger` and `IMidiRenderer` for dependency injection.
- `ConversionResult` and the `ScoreDocument` model serialize to JSON via the source-generated `YueToLogicJsonContext`, so a frontend can display the score without parsing MIDI.
- Problems are returned as `Diagnostic` records with stable codes (`YTL0xx`) instead of being logged or thrown.

```csharp
var result = new ScoreConverter().Convert(abcText);
if (result.Success)
{
    File.WriteAllBytes("song.mid", result.Midi!);
}
```

## Development

```sh
dotnet build
dotnet test
```

## Next steps

Producing a complete `.logicx` project. Logic's project format is an undocumented binary package, so the likely next step is an output folder with the MIDI file and the copied audio that can be imported into Logic in one go.
