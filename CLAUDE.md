# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Converts the `score.abc` that YuE2 writes next to its `audio.flac` into a Standard MIDI File and, together with the audio, into a Logic Pro project (`.logicx`) - and a MIDI file edited in Logic back into a `score.abc` for YuE2. Three hosts share one library: a CLI (`yue2logic`), an ASP.NET Core API with a Vue frontend under `/ui`, and a container image built by CI. README.md (English) and README.de.md (German) are the user documentation; both carry the same sections and are updated together.

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

Frontend only, from `src/YueToLogic.Api/ClientApp`: `npm run dev`, `npm run build` (vue-tsc type check, then Vite), `npm run type-check`, `npm run format` (Prettier, config in `.prettierrc.json`). There is no linter or JS test runner.

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
4. `MidiRenderer` (`Midi/`, DryWetMidi) – type 1 SMF: conductor track, one track per voice, chords, accompaniment; drums on channel 10.

`VoiceTrack.Kind` (`Melody`, `Chords`, `Bass`, `Drums`, `GuideTones`, `Doubling`) is how later stages and the Logic writer tell generated tracks apart. Track names (`Vocal`, `Ins`, `Chords`, `Bass`, `Drums`, `Guide`, `Vocal 8vb`, `Kick`…) are the key for MIDI channels, programs, instruments and Logic template tracks alike.

### The way back: MIDI to ABC

`MidiToAbcConverter` (`Conversion/`) reverses the pipeline for a MIDI file that came back from Logic: `MidiFileReader` (`Midi/`, pairs notes itself) → track roles by name (`Vocal`, `Ins`, `Chords`; Logic's "Part · Instrument" header is cut at the dot; generated tracks and channel 10 are ignored; unnamed files by sound and order) → sixteenth grid → one note at a time per voice → `ChordTrackReader`/`ChordRecognizer` (`Harmony/`) read the chord track back into symbols → leading silent bars dropped → a `ScoreDocument` → `AbcScoreWriter` (`Abc/`). The writer lays the score out exactly as YuE2 does; `AbcScoreWriterTests` and `MidiToAbcConverterTests` hold it to that with character-for-character round trips of `samples/score.abc` (also through MIDI with every arrangement option) and of the template song. The chord recognizer's scoring and its rules for inversion vs. slash chord are documented in its `<remarks>`; change them against the exhaustive voicing tests. Track indices in `MidiToAbcOptions.TrackRoles` are 0-based; the CLI's `--track` counts from 1.

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

Singing voice conversion is the same kind of optional proxy, to ChangeMyVoice (`Voice:BaseUrl`/`ApiKey`, `Core/Voices`): a collection of reference voices, edited like the instrument library, and jobs whose source is either the vocal stem of a finished stem job, fetched server-side (`VoiceEndpoints.StartJobAsync`, dry vocals preferred), or a WAV the user uploads (`file`, checked for a RIFF/WAVE header, no stem service needed). The Logic export takes a finished voice job (`voiceJob` form field); its WAV replaces the separated vocals on the project's vocals track. `VoiceImport` checks the sample rate first, because the Logic template needs 48 kHz and the model works at its own: anything else is left out of the project (the separated vocals stay, where there are any) with a warning (`YTL056`) rather than failing the export. `VoiceImport` also compares the result's SHA-256 against the checksum the job names, so a transfer that broke off does not reach the project. The job list is paged (`status`, `limit`, `offset` → `VoiceJobPage`), because the service keeps a record of every job it ever had; an older ChangeMyVoice without that route answers 404, which becomes a 501 here and the job dialog explains. Refusals carry the service's own `code` (`VoiceConversionCode`), which is what the messages are built from - three different things answer 409 - and the statuses 429 and 503 are passed through, since those are the ones worth trying again (`VoiceConversionException.CanRetryLater`). How the key is made and why the calls go server to server: `docs/ui-anbinden.md` in the ChangeMyVoice repository.

Warnings of a Logic export travel in the `X-YueToLogic-Diagnostics` header (compact JSON), since the body is the ZIP.

### Frontend (`src/YueToLogic.Api/ClientApp`)

Vue 3 + TypeScript + Vite, no router, no state library. `App.vue` holds the state; `api.ts` is the only place that talks to `/api`; `options.ts` maps the form to `ConversionOptions` (percent sliders map to the same maxima as the CLI flags); `i18n.ts` holds every German and English string; `player.ts` schedules preview playback to Web MIDI ports or a Web Audio fallback; `pianoRoll.ts` draws only the visible slice on a canvas. Web MIDI exists only in Chromium (`midiUsable()`), which gates reading the machine's outputs and playing through them - not the instrument library: that is server state and is edited everywhere, with the ports of the stored instruments as the choice where none can be looked up (a browser with neither cannot add the first one). Form state lives in `localStorage`; presets and instruments live on the server (`presets.ts` moves a browser's old local presets over once).

The look follows YuE UI. UI components come from PrimeVue 4 (styled mode, Aura preset from `@primeuix/themes`), registered globally in `main.ts`; the dialogs are PrimeVue `Dialog`s opened through each component's exposed `open()`. Use a PrimeVue component instead of styling native elements, and adjust the look through props, `pt` or utilities rather than by overriding `.p-*` classes. Tailwind CSS 4 (via `@tailwindcss/vite`, without preflight) with `tailwindcss-primeui` does the layout. Cascade layers decide what wins, declared at the top of `style.css`: `theme, base, primevue, components, utilities`; an unlayered rule, including every `<style scoped>` block, beats all of them. `style.css` maps the app's own variables (`--accent`, `--text`, `--border`, ...) onto Aura's tokens, so scoped styles and the piano roll's canvas (`pianoRoll.ts` reads them) share PrimeVue's palette in both themes.

## Conventions

- Commit messages and branch names are German; code, comments and diagnostics are English. Work happens on `feature/*` branches merged to `main` by pull request.
- Comments explain why and record what was learned (especially the Logic byte layouts); keep that style when touching those files.
- Every user-facing option exists three times: CLI flag (`CliArguments`, `CliText` in both languages), `ConversionOptions` JSON, and the web form (`options.ts`, `OptionsForm.vue`, `i18n.ts`). Add a diagnostic code in `DiagnosticCodes` rather than free-form messages, and document new flags and codes in both READMEs.
- `samples/score.abc` is the official YuE2 example and is copied into both test projects' output as `Samples/score.abc`.
