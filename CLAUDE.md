# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Converts the `score.abc` that YuE2 writes next to its `audio.flac` into a Standard MIDI File and, together with the audio, into a Logic Pro project (`.logicx`). Three hosts share one library: a CLI (`yue2logic`), an ASP.NET Core API with a Vue frontend under `/ui`, and a container image built by CI. README.md (English) and README.de.md (German) are the user documentation; both carry the same sections and are updated together.

## Commands

Requires the .NET 10 SDK; the web frontend additionally needs Node.js 22.12+.

```sh
dotnet build                     # whole solution (YueToLogic.slnx)
dotnet test                      # both test projects (xUnit)
dotnet test tests/YueToLogic.Core.Tests
dotnet test --filter "FullyQualifiedName~LogicProjectWriterTests"          # one class
dotnet test --filter "Name=Notes_are_encoded_exactly_as_logic_stores_them"  # one test

dotnet run --project src/YueToLogic.Cli -- samples/score.abc --bass --drums --dump-json out/sample
dotnet run --project src/YueToLogic.Api       # API on :5080, SpaProxy starts Vite on 127.0.0.1:5173/ui/

dotnet publish src/YueToLogic.Api -c Release -o publish   # runs npm ci + npm run build, ships wwwroot/ui
docker build -t yue-to-logic .
```

Frontend only, from `src/YueToLogic.Api/ClientApp`: `npm run dev`, `npm run build` (vue-tsc type check, then Vite), `npm run type-check`. There is no linter or JS test runner.

CI (`.github/workflows/ci.yml`) builds with `-warnaserror`, runs the tests, converts `samples/score.abc` with the CLI and checks the JSON dump with `jq`, then publishes with the frontend and builds the image. A warning that passes locally still fails CI.

Notes:
- A Debug build of the API project runs `npm ci` once when `ClientApp/node_modules` is missing. Pass `-p:SkipClientAppBuild=true` to skip every npm step (the Dockerfile does).
- The Api tests start the app with `WebApplicationFactory<Program>`; they do not need a built frontend.
- The CLI answers in German or English depending on the UI culture (`CliText`); there are no CLI tests, CI only checks its exit code and output files.

## Architecture

### Core library (`src/YueToLogic.Core`) is host-independent

No console, no file system, no logging. Input is a `string`/`Stream`, output `byte[]` or a caller-supplied `Stream`; problems come back as `Diagnostic` records with stable codes `YTL0xx` (`Diagnostics/DiagnosticCodes.cs`) instead of exceptions or logs. Keep it that way: it is meant to be reused from web, Electron and macOS hosts. It is `IsAotCompatible`, so JSON goes through the source-generated `YueToLogicJsonContext` (camelCase, enums as strings); every new type that crosses an HTTP boundary must be added there. The frontend's `ClientApp/src/types.ts` mirrors that contract by hand.

`AddYueToLogic()` registers everything as stateless singletons.

### Conversion pipeline

`ScoreConverter.Convert` runs, in order:

1. `ConversionOptionsValidator` – range checks shared by all hosts (error `YTL042`).
2. `AbcScoreParser` (`Abc/`) – reads YuE2's ABC dialect into a `ScoreDocument` (absolute ticks, voices, chords, sections, meter/key changes). Lenient: anything outside the dialect becomes a diagnostic, only a score that cannot be placed on a tick grid fails. Note the YuE2 rule that an accidental applies to its note letter in every octave until the bar line.
3. `ScoreArranger` (`Arrangement/`) – octave shifts, doubling, then generated tracks (chords, bass, drums, guide tones), then `GrooveProcessor` (swing/humanize), then `MonoProcessor`, and last `CountInBuilder`. Order matters and is documented in the class; the result is a new `ScoreDocument`, so JSON, MIDI and the Logic project always show the same notes.
4. `TempoFitter` – optional, after arrangement and before rendering so every output carries the fitted tempo. The library never measures audio; the host passes `fitTempo.audioSeconds` (CLI reads the FLAC header, the browser reads 42 bytes client-side).
5. `MidiRenderer` (`Midi/`, DryWetMidi) – type 1 SMF: conductor track, one track per voice, chords, accompaniment; drums on channel 10.

`VoiceTrack.Kind` (`Melody`, `Chords`, `Bass`, `Drums`, `GuideTones`, `Doubling`) is how later stages and the Logic writer tell generated tracks apart. Track names (`Vocal`, `Ins`, `Chords`, `Bass`, `Drums`, `Guide`, `Vocal 8vb`, `Kick`…) are the key for MIDI channels, programs, instruments and Logic template tracks alike.

### Logic Pro project writer (`Core/Logic`)

Logic's format is undocumented. `LogicProjectWriter` does not build a project; it patches a template saved by Logic Pro 12.3 (`Logic/Template/*`, embedded resources) whose `ProjectData` is parsed into chunks by `LogicProjectData`. The partial-class files each own one concern (Audio, GlobalTracks, Instruments, Regions, Signatures, Tracks) and their `<remarks>` record the byte offsets and how they were derived. Objects added beyond the template (extra chord regions, section regions) are cloned and registered in the object registry (`LogicObjectRegistry`) with time-based UUIDs (the writer takes an optional `TimeProvider`).

Consequences when changing this area:
- The template decides which tracks exist; a voice with no template track only reaches the MIDI file (`YTL053`). Track matching is by region name at bar 1.
- Routing a track to hardware swaps its software instrument for Apple's External Instrument (`ExternalInstrument.bin`), which only works for MIDI outputs the template's Mac knew (`YTL055`).
- `LogicProjectWriterTests` checks that regenerating the template's own song (`tests/.../Fixtures/logic-template-song.abc`) reproduces Logic's note bytes exactly, and that the template round-trips byte for byte. A new template must be saved from Logic, plists converted with `plutil -convert xml1`, and that test updated.
- Every change was verified by opening the result in Logic; there is no other oracle. Paths of the template's Mac are scrubbed from the output.

Output goes through `ILogicPackageSink` (`ZipLogicPackageSink` in the API, `DirectoryLogicPackageSink` in the CLI). Audio must be 48 kHz FLAC (mix) / WAV (stems).

### API (`src/YueToLogic.Api`)

Minimal API, endpoint groups per file (`ConvertEndpoints`, `StemEndpoints`, `VoiceEndpoints`, `InstrumentEndpoints`, `PresetEndpoints`, `ClientAppEndpoints`). Conversion is stateless. The only state is one SQLite file (`Data/SqliteDatabase`, configured by `Data:Path`, created on first use so a read-only server still converts) holding the instrument library (name → MIDI port + channel, plus track assignments; `SqliteInstrumentStore`) and the web form's presets (`SqlitePresetStore`, form kept as opaque JSON). Schema changes are versioned migrations in `SqliteDatabase.EnsureCreated` (`PRAGMA user_version`); add a step, never edit an old one. Presets belong to a user: there is no login yet, so `ICurrentUser` is the constant `local` user, and stores take the user id on every call so authentication can be added without touching them. Stem separation is an optional proxy to StemMyWav (`Stems:BaseUrl`/`ApiKey`); when unconfigured the endpoints answer 501 and the UI hides the section. The Logic export can fetch a finished stem job server-side (`stemJob` form field) instead of round-tripping WAVs through the browser.

Singing voice conversion is the same kind of optional proxy, to ChangeMyVoice (`Voice:BaseUrl`/`ApiKey`, `Core/Voices`): a collection of reference voices, edited like the instrument library, and jobs whose source is the vocal stem of a finished stem job, fetched server-side (`VoiceEndpoints.StartJobAsync`, dry vocals preferred). The Logic export takes a finished voice job (`voiceJob` form field); its WAV replaces the separated vocals on the project's vocals track. `VoiceImport` checks the sample rate first, because the Logic template needs 48 kHz and the model works at its own: anything else keeps the separated vocals and warns (`YTL056`) rather than failing the export. ChangeMyVoice has no route for all of its jobs yet; its 404 on the list becomes a 501 here, which the job dialog explains.

Warnings of a Logic export travel in the `X-YueToLogic-Diagnostics` header (compact JSON), since the body is the ZIP.

### Frontend (`src/YueToLogic.Api/ClientApp`)

Vue 3 + TypeScript + Vite, no router, no state library, no component framework. `App.vue` holds the state; `api.ts` is the only place that talks to `/api`; `options.ts` maps the form to `ConversionOptions` (percent sliders map to the same maxima as the CLI flags); `i18n.ts` holds every German and English string; `player.ts` schedules preview playback to Web MIDI ports or a Web Audio fallback; `pianoRoll.ts` draws only the visible slice on a canvas. Web MIDI exists only in Chromium, so instrument routing is editable there and read-only elsewhere (`midiUsable()`). Form state lives in `localStorage`; presets and instruments live on the server (`presets.ts` moves a browser's old local presets over once).

## Conventions

- Commit messages and branch names are German; code, comments and diagnostics are English. Work happens on `feature/*` branches merged to `main` by pull request.
- Comments explain why and record what was learned (especially the Logic byte layouts); keep that style when touching those files.
- Every user-facing option exists three times: CLI flag (`CliArguments`, `CliText` in both languages), `ConversionOptions` JSON, and the web form (`options.ts`, `OptionsForm.vue`, `i18n.ts`). Add a diagnostic code in `DiagnosticCodes` rather than free-form messages, and document new flags and codes in both READMEs.
- `samples/score.abc` is the official YuE2 example and is copied into both test projects' output as `Samples/score.abc`.
