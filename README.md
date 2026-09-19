# YuE to Logic

[![CI](https://github.com/Marcel-B/yue-to-logic-pro/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcel-B/yue-to-logic-pro/actions/workflows/ci.yml)

[Deutsche Version](README.de.md)

[YuE](https://github.com/multimodal-art-projection/YuE) generates music with AI. Besides the audio (`audio.flac`), a YuE2 run with chain-of-thought mode `full` or `melody` writes a symbolic version of the song: `score.abc`. That file contains the tempo, meter, key, song structure (verse, chorus, …), a vocal and an instrumental melody, and the chord progression.

The long-term goal of this project is a Logic Pro project with the generated audio as a region and matching MIDI tracks below it. It is meant as a starting point for analysing, editing or extending a song, not as a perfect transcription.

**Current state:** converts `score.abc` into a MIDI file and, together with the YuE audio, into a Logic Pro project — from the command line or in a web interface.

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
| `--drum-pattern <p>` | Drum groove: `four-on-the-floor` (default), `backbeat`; implies `--drums` |
| `--ppq <n>` | MIDI resolution in ticks per quarter note (default 480) |
| `--logic <audio.flac>` | Also write a Logic Pro project `<output>.logicx` with all tracks and this audio (see below) |
| `--dump-json <file>` | Also write the parsed score and all diagnostics as JSON; `.json` is appended if missing |
| `-f, --force` | Overwrite existing output files |
| `-v, --verbose` | Also show informational messages |

Exit codes: `0` success, `1` the score could not be converted, `2` invalid arguments or a file error. The CLI answers in German or English depending on the system language.

### In Logic Pro

Preferably open the MIDI file with *File → Open*: Logic then creates a new project that takes tempo, meter and markers from the file, starting at bar 1. When dragging the file into an existing project instead, drop it exactly at bar 1 and confirm importing the tempo; tempo and meter are placed relative to the drop position, so everything before it keeps the project tempo. Then drag `audio.flac` from the same YuE output folder onto a new audio track at bar 1. Because the MIDI file carries the tempo from the score, both line up.

## Web interface

A Vue frontend lets you drop a `score.abc` (or pick it with a file dialog), set the same parameters as the CLI, and download the MIDI file and the JSON dump. It also shows tempo, meter, key, length, the song sections and all diagnostics.

The frontend lives in `src/YueToLogic.Api/ClientApp` and is delivered by the API under `/ui` (`/` redirects there). Node.js 22.12 or later is needed in addition to .NET.

**Development:** one command starts everything:

```sh
dotnet run --project src/YueToLogic.Api
```

The first build installs the npm packages. On start, [SpaProxy](https://learn.microsoft.com/aspnet/core/client-side/spa/intro) launches the Vite dev server (`npm run dev`) and the browser opens on <http://localhost:5080>, which forwards to Vite at <http://127.0.0.1:5173/ui/>. Changes to the Vue code appear immediately; Vite forwards `/api` back to the API.

**Deployment:** `dotnet publish` builds the frontend (`npm ci`, `npm run build`) and ships it as `wwwroot/ui`:

```sh
dotnet publish src/YueToLogic.Api -c Release -o publish
dotnet publish/YueToLogic.Api.dll --urls http://localhost:5080
```

If the frontend is built separately, for example in its own Docker stage, pass `-p:SkipClientAppBuild=true` and copy `ClientApp/dist` to `wwwroot/ui`.

### HTTP API

| Endpoint | Request | Response |
|---|---|---|
| `POST /api/convert` | multipart form: `file` (the score), optional `options` (JSON, see below) | `200` with score, diagnostics and `midi` (base64) as JSON; `422` with diagnostics if the score or options cannot be used; `400` for a missing file or malformed options |
| `POST /api/convert/midi` | same | the MIDI file (`audio/midi`) |
| `POST /api/convert/logic` | as above, plus `audio` (the `audio.flac`, up to 250 MB) and optional `name` | a ZIP with `<name>.logicx`; warnings in the `X-YueToLogic-Diagnostics` header; `422` if the audio is not a 48 kHz FLAC |
| `GET /api/health` | – | `ok` |

`options` is the JSON form of `ConversionOptions`; every field is optional:

```json
{
  "ticksPerQuarterNote": 480,
  "includeChordTrack": true,
  "arrangement": {
    "defaultOctaveShift": 0,
    "octaveShifts": { "Vocal": -1 },
    "bass": { "pattern": "Eighths", "octaveShift": 0 },
    "drums": { "pattern": "Backbeat", "crashOnSections": true }
  }
}
```

```sh
curl -F file=@score.abc -F 'options={"arrangement":{"drums":{}}}' http://localhost:5080/api/convert/midi -o score.mid
```

Clients served from another origin (for example an Electron shell) must be listed in `Cors:AllowedOrigins` in `appsettings.json`. The OpenAPI description is available at `/api/openapi`.

## Logic Pro project (experimental)

With the YuE `audio.flac` (CLI `--logic`, web interface: second drop zone, then *Download Logic project*) the tool builds a complete Logic Pro project: the audio on track 1 at bar 1, the tracks Vocal, Ins, Chords, Bass and Drums as MIDI regions, the song sections as arrangement markers and every chord on Logic's chord track (which Session Players can follow), with tempo, meter and project length taken from the score.

Logic's project format is undocumented. The project is therefore built from a template saved by Logic Pro 12.3 (`src/YueToLogic.Core/Logic/Template`), whose notes, lengths, tempo, meter, markers, chords and audio are replaced; chord regions and marker names beyond the template's are added as new objects, registered the way Logic does it. The instruments chosen in that template are used for every project. The format was analysed and every change verified by opening, editing, saving and reopening the result in Logic. Limitations: only the first meter of a score is used, the audio must be 48 kHz, and the chord scales offered to Session Players are a default per chord type. A future Logic version may need a newly saved template.

To use your own sounds, create a template the same way: open a MIDI file from this tool in Logic (*File → Open*), drag `audio.flac` onto a new audio track at bar 1, choose instruments, add at least one arrangement marker and one chord on the chord track, save as a package with audio copied into the project, and replace the files in `Logic/Template` (`MetaData.plist` and `ProjectInformation.plist` converted with `plutil -convert xml1`).

## Container and deployment

CI builds a container image with API and web interface and pushes it to the GitHub Container Registry once tests and publish have passed:

| Tag | Meaning |
|---|---|
| `ghcr.io/marcel-b/yue-to-logic-pro:latest` | newest commit on `main` |
| `…:sha-3f2c1ab` | a specific commit |
| `…:1.2.0`, `…:1.2` | a release, created by pushing a tag: `git tag v1.2.0 && git push origin v1.2.0` |

The image listens on port 8080, runs as an unprivileged user and keeps no state. To try it locally: `docker build -t yue-to-logic . && docker run --rm -p 8080:8080 yue-to-logic`, then open <http://localhost:8080>.

### Proxmox container with Docker Compose

1. **Container:** a Debian LXC container. For Docker inside an unprivileged container, enable *Options → Features → nesting* and *keyctl*. Install Docker Engine with the Compose plugin as described at <https://docs.docker.com/engine/install/debian/>.
2. **Files:** copy [`deploy/compose.yml`](deploy/compose.yml) and [`deploy/.env.example`](deploy/.env.example) to e.g. `/opt/yue-to-logic/`, rename `.env.example` to `.env` and adjust it.
3. **Start:** `docker compose pull && docker compose up -d`, check with `curl http://localhost:8080/api/health` (answers `ok`).
4. **Nginx Proxy Manager:** *Hosts → Proxy Hosts → Add Proxy Host*
   - *Details:* Domain Names `music.idsrv.info`, Scheme `http`, Forward Hostname/IP = IP of the container, Forward Port `8080`, *Block Common Exploits* on.
   - *SSL:* choose or request a certificate (for a host that is only reachable privately, Let's Encrypt needs the DNS challenge, or use an existing `*.idsrv.info` wildcard certificate); enable *Force SSL* and *HTTP/2 Support*.
   - NPM sets the `X-Forwarded-*` headers itself. For the Logic export, uploads of 45–100 MB audio must pass: if NPM answers `413 Request Entity Too Large`, add `client_max_body_size 300m;` under *Advanced*.
5. **AdGuard Home:** under *Filters → DNS rewrites* add `music.idsrv.info` → IP of the **Nginx Proxy Manager** host (not of the app container).
6. Once <https://music.idsrv.info> works, set `ALLOWED_HOSTS=music.idsrv.info;localhost` in `.env` and run `docker compose up -d` again.
7. **Update:** `docker compose pull && docker compose up -d`.

`https://music.idsrv.info/` redirects to the interface at `/ui/`. If a browser keeps showing another page there (typically one cached while the proxy host was being set up), clear the site data for `music.idsrv.info` or try a private window.

The container runs with a read-only file system, without Linux capabilities and with `no-new-privileges`. GitHub may create the image package as *private* on first push even though the repository is public: either set it to public once under *Packages → yue-to-logic-pro → Package settings*, or run `docker login ghcr.io` on the host with a token that has `read:packages`.

## What the MIDI file contains

A Standard MIDI File, type 1:

| Track | Content |
|---|---|
| `Conductor` | Tempo, meter, key and a marker for every section (`verse`, `chorus`, …); Logic puts these into its global tracks |
| `Vocal` | The vocal melody |
| `Ins` | The instrumental melody |
| `Chords` | The chord symbols played as block chords (root in octave 3, slash bass below), plus the symbol as a text event |
| `Bass` | Only with `--bass`: the bass note of every chord in the register E2–D♯3 (MIDI 40–51, in Logic's naming E1–D♯2), which every bass instrument can play; `--bass-octave -1` goes down to the lowest bass-guitar string. Slash chords such as `C/E` play their bass note. Notes are slightly detached, on-beat notes a little louder. `root-fifth` alternates quarter notes between bass note and the chord's fifth |
| `Drums` | Only with `--drums`: *four on the floor* plays the kick on every beat, *backbeat* on 1 and 3 with an open hi-hat on the last eighth (4+). Both play the snare on 2 and 4, a closed hi-hat in eighth notes (off-beats softer) and a crash cymbal at the start of every section. General MIDI note numbers on channel 10, which Logic's drum kits understand. In 3/4 the snare plays on beat 2; 6/8 is counted in dotted quarters |

The arrangement options are also available in the library (`ConversionOptions.Arrangement`), and the generated tracks appear in the JSON output with `"kind": "Bass"` or `"Drums"`.

## The input format

YuE2 writes a deliberately small subset of ABC notation. A generic ABC parser would misread it: most importantly, an accidental applies to its note letter **in every octave** until the bar line (after `^F`, `f` is sharp too). The parser in this project follows the YuE2 rules and reports everything outside them as a diagnostic instead of failing, so hand-edited scores still convert. The full rules are described in [YuE2's ABC editing reference](https://github.com/multimodal-art-projection/YuE/blob/main/skills/yue2-music/references/abc-editing.md).

## Project layout

```
src/YueToLogic.Core/    Library: ABC parsing, score model, MIDI rendering
src/YueToLogic.Cli/     Command-line tool (yue2logic)
  Logic/                Logic Pro project writer and the embedded template
src/YueToLogic.Api/     ASP.NET Core API; serves the web frontend under /ui
  ClientApp/            Vue 3 + Vite + TypeScript frontend
deploy/                 Docker Compose setup for the server
Dockerfile              Container image (API + frontend)
tests/                  xUnit tests
samples/score.abc       Official YuE2 example score
```

`YueToLogic.Core` has no console or file-system dependencies so that it can later be used from a web service, an Electron/Vue frontend or a macOS app:

- Input is a `string` or `Stream`, output is a `byte[]` or a caller-supplied `Stream`.
- Option ranges are checked by `ConversionOptionsValidator`, so every host accepts the same values.
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
dotnet publish src/YueToLogic.Api -c Release -o publish   # includes type check and bundle of the frontend
```

The GitHub Action in `.github/workflows/ci.yml` runs the same steps on every push and pull request.

## Next steps

- Logic projects without audio.
- Meter changes within a song in the Logic project.
- Key signature in the Logic project.
