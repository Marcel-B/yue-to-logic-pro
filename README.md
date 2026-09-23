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
| `--bass-pattern <p>` | Bass rhythm: `eighths` (default), `quarters`, `root-fifth`, `octaves`, `offbeat`, `sustained`, `walking`; implies `--bass` |
| `--bass-octave <n>` | Move the bass by `n` octaves (−2 to 2); implies `--bass` |
| `--drums` | Add a drum track (see below) |
| `--drum-pattern <p>` | Drum groove: `four-on-the-floor` (default), `backbeat`, `half-time`, `disco`, `sixteenth-hats`, `shuffle`; implies `--drums` |
| `--no-crash` | No crash cymbal at the start of a section |
| `--split-drums` | One track per drum: `Kick`, `Snare`, `HiHat`, `Crash`; implies `--drums` |
| `--drum-note <drum>=<note>` | Note a drum machine plays a drum on: `kick`, `snare`, `closed-hihat`, `open-hihat`, `crash`, `clap`; the note as a number (`36`) or as Logic names it (`C1`); repeatable, the rest stay General MIDI; implies `--drums` |
| `--chord-pattern <p>` | How the chords are played: `block` (default, as written), `eighths`, `sixteenths`, `offbeat`, `arpeggio`, `arpeggio-up-down` |
| `--channel <track>=<n>` | Fixed MIDI channel 1–16 for a track (`Vocal`, `Ins`, `Chords`, `Bass`, `Drums`, `Guide`, `Vocal 8vb`); repeatable |
| `--program <track>=<n>` | Program change 1–128 at the start of a track; repeatable |
| `--chord-voicing <v>` | Inversion of the chords: `root` (default), `closest`, `first`, `second` (see below) |
| `--chord-octave <n>` | Move the chord track by `n` octaves (−2 to 2) |
| `--guide-tones` | Add a held track of every chord's third and seventh (see below) |
| `--guide-octave <n>` | Move that track by `n` octaves (−2 to 2); implies `--guide-tones` |
| `--double-vocal` | Double the vocal melody an octave below, on a track of its own |
| `--double-octave <n>` | Octave of that copy (−2 to 2, default −1); implies `--double-vocal` |
| `--swing <n>` | Swing in percent: `0` straight (default), `100` a full triplet feel |
| `--swing-unit <u>` | Which subdivision swings: `eighths` (default), `sixteenths` |
| `--straight-drums` | Keep the drums on the grid while everything else swings |
| `--humanize <n>` | Scatter timing and velocity by `n` percent (0 default; 100 moves a note by up to 25 ms) |
| `--mono` | Prepare the melodies for monophonic synthesizers (see below) |
| `--legato` | As `--mono`, and every note reaches to the next one |
| `--logic-split-sections` | One region per song section in the Logic project instead of one per track |
| `--count-in <n>` | Silent bars in front of the song (0 to 8), with a click on every beat (see below) |
| `--count-in-silent` | No click in those bars; implies `--count-in 1` |
| `--fit-tempo` | Adjust the tempo so the score lasts as long as the audio given with `--logic` (see below) |
| `--ppq <n>` | MIDI resolution in ticks per quarter note (default 480) |
| `--logic <audio.flac>` | Also write a Logic Pro project `<output>.logicx` with all tracks and this audio (see below) |
| `--logic-no-audio` | Also write a Logic Pro project without audio; its audio track stays empty |
| `--vocals <file>` | Separated vocals (WAV) for the second audio track of the Logic project |
| `--vocals-dry <file>` | Separated vocals without reverb (WAV) for the third audio track |
| `--dump-json <file>` | Also write the parsed score and all diagnostics as JSON; `.json` is appended if missing |
| `-f, --force` | Overwrite existing output files |
| `-v, --verbose` | Also show informational messages |

Exit codes: `0` success, `1` the score could not be converted, `2` invalid arguments or a file error. The CLI answers in German or English depending on the system language.

### In Logic Pro

Preferably open the MIDI file with *File → Open*: Logic then creates a new project that takes tempo, meter and markers from the file, starting at bar 1. When dragging the file into an existing project instead, drop it exactly at bar 1 and confirm importing the tempo; tempo and meter are placed relative to the drop position, so everything before it keeps the project tempo. Then drag `audio.flac` from the same YuE output folder onto a new audio track at bar 1. Because the MIDI file carries the tempo from the score, both line up.

## Web interface

A Vue frontend lets you drop a `score.abc` (or pick it with a file dialog), set the same parameters as the CLI, and download the MIDI file and the JSON dump. It also shows tempo, meter, key, length, the song sections and all diagnostics.

Instead of two single files you can drop a **whole YuE output folder**, or open it with *Choose folder*: `score.abc` and `audio.flac` are looked for inside, one level down in `song1`, `song2` and so on as well. With several songs in the folder the first one is taken and the number of the others is reported.

**Presets** above the parameter list save the whole set under a name and bring it back — for the combination of patterns, registers, groove and MIDI channels you usually work with. They are kept on the server, like the [instruments](#instruments), so every browser you open the interface from offers the same ones, and they survive *Reset*, which only clears the form. Presets a browser saved before they moved to the server are handed over the first time it sees an empty server.

### Preview

Every conversion is shown as a piano roll: a bar ruler with the song sections, the chord symbols, and one lane per track, zoomable and scrollable. It answers the question the parameters keep raising - does the register fit, does the pattern fit, do the chords sit where they should - without the detour through Logic. The drawing is done on a canvas and only for the visible slice, so a song with several thousand notes still scrolls smoothly.

The preview also plays. Each track is routed on its own, to a MIDI port and channel or to the browser's own sound:

- **MIDI** sends to real instruments. Every track has its own port and channel (1-16, as the device labels them), so a rack of synthesizers can be driven where each one listens. A *Test* button per track sends a single note, which is the quickest way to tell a wiring problem from a routing one, and *Panic* lifts every key on every output when an instrument hangs. The routing is remembered per track name, so a fixed setup is entered once.
- **Browser sound** needs no hardware: a sawtooth with an envelope for pitched tracks and filtered noise for the drums. It is a rough stand-in, enough to check timing, swing and register.

Web MIDI is only available over HTTPS or on localhost, and only in browsers that implement it - Chrome does, Safari does not, where the preview falls back to the browser sound. Chrome asks for permission on first use, so the preview requests access only when you press *Find MIDI devices*.

**Instruments** save entering ports and channels at all. Under *Instruments* (the sliders icon in the header, so it works before a score is loaded) each synthesizer gets a name for its port and channel - "WASP Deluxe" for "Scarlett 8i6 USB", channel 1, "Mother32" for "MIDI4x4 Midi Out 1", channel 12. The routing table then gets an *Instrument* column: choose one and the track goes to that port and channel, choose none and the port and channel selects are back. Both the instruments and which track plays which are kept on the server, so every browser you open the interface from sees the same setup; the ports are stored by name, so an instrument whose interface is unplugged keeps its entry and plays through the browser sound until it is back. The choice also goes into the downloads: the MIDI file and the Logic project put the track on the instrument's channel, and the Logic track is named after both (*Bass · Mother32*). An instrument is either a *synthesizer* or a *drum machine*: a drum machine plays several drums on its one channel, each on a note of its own, so it also takes the note of its kick, snare, closed and open hi-hat, crash and clap - typed as Logic names them (`C1`) or as numbers. The drum tracks that play it are then generated on those notes, so the kick region really triggers its kick: with one `Drums` track from the machine on that track, with a split kit each of `Kick`, `Snare`, `HiHat` and `Crash` from its own, and the count-in clicks with the clap. Without a drum machine the drums stay on General MIDI, as before. Only one part of this needs Web MIDI: reading the outputs of the machine and playing through them, which the Chromium engine (Chrome, Edge) alone can do. The library itself lives on the server, so in Safari and Firefox instruments can still be assigned to tracks and added — the *Instrument* column and the dialog work as everywhere, and the MIDI file and the Logic project follow what is assigned there. What differs: the output of a new instrument can only be picked among those the stored instruments already use, since the browser cannot look any up, and the preview sounds through the browser rather than through the devices. Without a single stored output there is nothing to point at, so the first instrument has to be added in Chrome or Edge once; a note in the dialog says so.

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
| `POST /api/convert/logic` | as above, with optional `audio` (the `audio.flac`, up to 250 MB), `name`, `splitSections` (`true` for one region per song section) and `instruments` (JSON: track name → `{ "name", "port", "channel" }`, see [Instruments](#instruments)) | a ZIP with `<name>.logicx`; warnings in the `X-YueToLogic-Diagnostics` header; `422` if the audio is not a 48 kHz FLAC |
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
    "drums": { "pattern": "Backbeat", "crashOnSections": true },
    "chords": { "pattern": "Offbeat", "inversion": "Closest", "octaveShift": 0 },
    "guideTones": { "octaveShift": 0, "velocity": 64 },
    "doubling": { "voiceId": "Vocal", "semitones": -12, "velocity": 80 },
    "groove": {
      "swing": 0.55,
      "swingUnit": "Eighths",
      "humanizeTimingMs": 10,
      "humanizeVelocity": 8,
      "seed": 1,
      "includeDrums": true
    },
    "mono": { "gapMs": 12, "minimumLengthMs": 40, "legato": false, "includeBass": true },
    "countIn": { "bars": 1, "click": true }
  },
  "fitTempo": { "audioSeconds": 352.68, "maxDeviation": 0.05 }
}
```

`splitSections` is a property of the Logic project rather than of the score, so it is its own form field instead of part of `options`.

```sh
curl -F file=@score.abc -F 'options={"arrangement":{"drums":{}}}' http://localhost:5080/api/convert/midi -o score.mid
```

Clients served from another origin (for example an Electron shell) must be listed in `Cors:AllowedOrigins` in `appsettings.json`. The OpenAPI description is available at `/api/openapi`.

## Logic Pro project (experimental)

With the YuE `audio.flac` (CLI `--logic`, web interface: second drop zone, then *Download Logic project*) the tool builds a complete Logic Pro project: the audio on track 1 at bar 1, the tracks Vocal, Ins, Chords, Bass and Drums as MIDI regions, the song sections as arrangement markers and every chord on Logic's chord track (which Session Players can follow), plus tempo, key, every meter change and the project length taken from the score. The stems of a separation go on audio tracks of their own: the template has three, for the recording, the separated vocals and the vocals without their reverb. Each track is named after what it plays (*Mix*, *Vocals*, *Vocals dry*), and a track without a file leaves the project so that Logic misses nothing. Without audio (CLI `--logic-no-audio`, web interface: simply leave the `audio.flac` out) you get the same project with an empty audio track, ready for the FLAC to be dropped onto later; the template's audio file object and its region are removed, as Logic would otherwise report a missing file when opening the project.

With `--logic-split-sections` (web interface: *One region per section instead of one per track*) every track is cut at the section starts instead of running as one region: the regions are named after the section they cover, numbered when a name comes back (`Verse 1`, `Chorus 1`, `Verse 2`, …), so a section can be copied, looped, muted or moved on its own. A track the score has nothing for keeps its single empty region, and a note reaching past a section border keeps its length — the region grows with it rather than the note being cut. The audio stays one region at bar 1.

Logic's project format is undocumented. The project is therefore built from a template saved by Logic Pro 12.3 (`src/YueToLogic.Core/Logic/Template`), whose notes, lengths, tempo, meter, markers, chords and audio are replaced; chord regions and marker names beyond the template's are added as new objects, registered the way Logic does it. The instruments chosen in that template are used for every project. The format was analysed and every change verified by opening, editing, saving and reopening the result in Logic. Limitations: the audio must be 48 kHz, and the chord scales offered to Session Players are a default per chord type. A future Logic version may need a newly saved template.

Each track is named after the part it carries — `Vocal`, `Ins`, `Chords`, `Bass`, `Drums` — rather than after the instrument the template happens to use, since the regions now carry the section names instead. Logic keeps a track's name on its channel strip, so this renames those; the audio track and the output bus keep theirs. A track with an [instrument](#instruments) is named after both, *Bass · Mother32*, its notes carry the instrument's channel, and in place of the template's software instrument the track gets Logic's *External Instrument*, switched on and set to the instrument's output and channel, with the track's MIDI input off and without the inserts that came with the template's sound - the project plays the hardware as soon as it opens. Logic finds an output by the unique id the Mac gave it, so only outputs the template's Mac had when the template was saved can be routed to (the template lists the Scarlett 8i6, the four MIDI4x4 outputs and the CME WIDI Bluetooth output); an instrument on any other output keeps the software instrument, and a warning (`YTL055`) says so. A newly saved template takes new interfaces along.

**Your own sounds, and further tracks.** The tracks a project has come from the template, so adding one there adds it everywhere. Convert your score once with the tracks you want (`--guide-tones`, `--double-vocal`), open the MIDI file in Logic (*File → Open*), which names the tracks after it, and build the template from that: drag `audio.flac` onto a new audio track at bar 1, choose instruments, add at least one arrangement marker and one chord on the chord track, save as a package with audio copied into the project, and replace the files in `Logic/Template` (`MetaData.plist` and `ProjectInformation.plist` converted with `plutil -convert xml1`). A track is matched to a voice by name, so keep the names the MIDI file gave them. Use ordinary software instrument tracks, with or without an instrument chosen: a Drum Machine Designer track is an aux with its sounds on strips of its own, so an [instrument](#instruments) cannot be routed to it (`YTL053` says so). What the template sounds like does not matter: sampled instruments and reverbs with impulse responses remember where their files were, but every project written is cleared of those paths — Logic finds its own content by itself.

## Stems (optional)

On request the web interface sends the `audio.flac` to [StemMyWav](https://github.com/Marcel-B/StemMyWav) and gets it back split into vocals and instrumental. The separation runs on a Mac with a Metal GPU and takes minutes, depending on the length. The flow is therefore asynchronous: the job is created, the interface asks for its state every five seconds and says so in a window once it is done. There you choose whether the Logic project is downloaded with the stems right away, whether the stems are discarded, or whether you take them later with the usual button. Nothing is downloaded on its own; the stems stay at the service until they have gone into a project or you delete them. Whoever wants them separately downloads the ZIP. The switch *Separate the reverb from the vocals* adds `vocals_dry.wav` and `vocals_reverb.wav`.

**Which model separates.** The service offers several separation models, and the list *Model* above the button holds what it knows; the interface fetches it rather than knowing it, so a gateway with new models needs no change here. Under the list stands what the chosen one does: which stems come back, and how long it computes measured against the playing time — a model at `realtimeFactor` 0.3 takes about thirteen minutes for a four-minute song, one at 2.5 about a minute and a half. The service's own default is marked and preselected, and the last choice is remembered in the browser. A model that separates no vocals — an instrumental or a drum model — says so: its stems can be downloaded as a ZIP, but the project's stem tracks stay empty, and the reverb switch, which needs a vocal stem, is not available with it.

The API key stays on the server: the browser only ever talks to this application, which passes the requests on.

| Endpoint | Request | Response |
|---|---|---|
| `GET /api/stems` | – | `{"available":true}` when a stem service is configured |
| `GET /api/stems/models` | – | the separation models of the service, each with `id`, `name`, `stems`, `speed`, `realtimeFactor` and `isDefault` |
| `POST /api/stems?dereverb=false&model=` | the raw FLAC as the body (`Content-Type: audio/flac`) | job with `id`, `status` and the `model` it runs with |
| `GET /api/stems/{id}` | – | `queued`, `processing`, `completed` or `failed`, with `lastError` and `model` |
| `GET /api/stems/{id}/result` | – | the ZIP with the WAV stems |
| `DELETE /api/stems/{id}` | – | confirms the import; the service removes result and job. Cancels a job that is still `queued`; `409` while it is being transferred to the Mac |
| `GET /api/stems/jobs` | – | every job the service knows, newest first, with `createdUtc` and `updatedUtc` |

**Clearing the queue.** The service takes only a couple of waiting jobs and answers *queue full* beyond that, and it has no interface of its own. The stem service dialog (the waveform icon in the header, shown only when a stem service is configured) lists what it knows, refreshes every five seconds while open, marks the job of this browser tab, and has a button per job: *Cancel* for a waiting one, *Delete* for a finished or failed one; a job on its way to the Mac cannot be removed until that is over. A job removed there that this tab was waiting for is let go of here as well.

The stems need not travel through the browser: the Logic export takes a field `stemJob` with the job id, and the server then fetches the WAV files from the stem service itself and puts them on the project's audio tracks. It confirms the import afterwards, whereupon the service removes its files. If a job cannot be fetched the project is written all the same — without stems and with a warning (`YTL054`).

It is configured with `Stems:BaseUrl` and `Stems:ApiKey` (`Stems__BaseUrl` and `Stems__ApiKey` in the container, see [`deploy/.env.example`](deploy/.env.example)). Without either of them the endpoints answer `501` and the interface leaves the section out. With the gateway on the same docker host the address is `http://stemmywav:8080`; this compose project then has to join its network (prepared, commented out, in [`deploy/compose.yml`](deploy/compose.yml)).

## Voice (optional)

A separation that is done can be sung by someone else: the vocal stem goes to ChangeMyVoice, which keeps melody, phrasing and performance and gives them the timbre of a stored voice. That runs on a Mac too and takes minutes, so it goes the same way as a separation — the job is created, the interface asks for its state every five seconds and says so in a window once it is done. There you choose whether the Logic project is downloaded with the new voice right away, whether the result is discarded, or whether you take it later with the usual button. Nothing is downloaded on its own; whoever wants the vocals separately fetches the WAV.

The order is therefore: `audio.flac`, *Create stems*, and once they are there choose a voice under *Change the voice* and start it. The vocals do not travel through the browser — it passes the id of the separation and the id of the voice, and the server fetches the vocal stem from the stem service and hands it on. Where the separation also made dry vocals, those are taken: the model copies what it hears, and a reverb that is sung along with stays in the result.

**The collection of model voices.** A model voice is one recording of a voice, kept by the service under a name — the collection a conversion chooses its timbre from, as the instrument library is what tracks are routed with. The dialog *Model voices* (the microphone in the header, shown only when a voice service is configured) lists what the service has, with the properties of the recording as it was uploaded and as the service keeps it, and adds one from a name and a file (WAV, MP3, FLAC, M4A/AAC or OGG/Opus). The service keeps the first 25 seconds as mono PCM at 44.1 kHz, since that is all the model uses; clean, dry singing without accompaniment gives the best result. A voice that a job is still waiting for cannot be removed, which the service refuses with `409`.

**The jobs.** The second microphone icon opens the voice service's jobs: what it knows, refreshed every five seconds while the window is open, this browser tab's job marked, and a button per job — *Cancel* for one that is waiting or running, *Remove* for a finished one, whose result is gone afterwards. A job removed there that this tab was waiting for is let go of here as well. A job that has been cleared away stays on at the service as a record, so the list only grows: it comes twenty-five at a time, *Back* and *Next* page through it, the line beside them says which part of how many is shown, and the select narrows it to one state — which is how one looks for what is waiting or what failed. Not every ChangeMyVoice offers the route for all of its jobs; one that does not answers `404`, which this server passes on as `501` and the window explains, instead of showing an empty list.

The API key stays on the server here as well: the browser only ever talks to this application, which passes the requests on.

| Endpoint | Request | Response |
|---|---|---|
| `GET /api/voice` | – | `{"available":true}` when a voice service is configured |
| `GET /api/voice/voices` | – | the collection, each voice with `id`, `label`, `createdUtc` and the properties of the `stored` and the `original` recording |
| `POST /api/voice/voices` | form with `label` and `file` | `201` with the voice; `400` when one of the two is missing |
| `DELETE /api/voice/voices/{id}` | – | `204`; `409` while a job still waits for the voice |
| `POST /api/voice/jobs` | form with `voiceId` and `stemJob` (the id of a finished separation) | `202` with the job; `400` without one of the two, `501` without a stem service |
| `GET /api/voice/jobs/{id}` | – | `QUEUED`, `RUNNING`, `COMPLETED`, `FAILED` or `CANCELLED`, with `voiceLabel`, the timestamps and, on a failure, `errorCode` and `errorMessage` |
| `GET /api/voice/jobs/{id}/result` | – | the converted recording as a WAV |
| `DELETE /api/voice/jobs/{id}` | – | cancels a job, or removes a finished one's result; repeatable |
| `GET /api/voice/jobs?status=&limit=&offset=` | – | one page of the jobs, newest first: `{ "jobs": [...], "total", "limit", "offset" }`. `limit` is 1 to 200 (the service takes 50 without one), `status` narrows to `QUEUED`, `RUNNING`, `COMPLETED`, `FAILED` or `CANCELLED`; `501` from a service that cannot list them |

The converted vocals need not travel through the browser either: the Logic export takes a field `voiceJob` with the job id, and the server fetches the WAV itself and puts it on the project's vocals track — in place of the separated vocals, while the dry ones, where a separation made them, keep their own track. Afterwards it confirms the import, whereupon the service drops the result. Two things are checked before the file goes into the package, and each of them leaves the project with the separated vocals and a warning (`YTL056`) rather than failing an export that is otherwise fine: the checksum the service names for its result, so that a transfer which broke off does not end up in the project as a truncated file, and the sample rate, since the project's audio tracks are prepared for 48 kHz while ChangeMyVoice works at its model's own. In both cases the result stays at the service, and so does the job in the interface: the *Change the voice* section keeps its download, says that the vocals did not go into the project, and points at the warnings of the export for the reason. The WAV can then be dropped onto the project's vocals track in Logic by hand. At the time of writing ChangeMyVoice answers with 44.1 kHz, so this is the way it goes until the service delivers 48 kHz.

The result is not kept forever: an hour after it was first fetched, and at most a day after the job, the service clears it away. Whatever is to be kept is therefore downloaded — either into the Logic project or as a WAV.

What the service refuses it explains in a `code` of its problem document, which says more than the status: a `409` is a name that is taken (`DUPLICATE_VOICE_LABEL`), a voice a job still waits for (`VOICE_IN_USE`) or a result that is not there yet (`RESULT_NOT_READY`). Those codes become the message the interface shows. The status is passed on as the service gave it, because it decides what to do next: `429` (the rate limit of this key) and `503` (`QUEUE_FULL`, or the converting Mac being away) are worth trying again later, which the interface says, while a recording the service cannot use never becomes usable by sending it again.

It is configured with `Voice:BaseUrl` and `Voice:ApiKey` (`Voice__BaseUrl` and `Voice__ApiKey` in the container, see [`deploy/.env.example`](deploy/.env.example)). Without either of them the endpoints answer `501` and the interface leaves the section, both dialogs and their icons out. The key is created at the gateway with `scripts/neuer-zugang.sh <name> <requests per minute>` and entered in its `clients.json`, one entry per application, so that a single access can be withdrawn and the logs say who started what; [*Eine externe Oberfläche anbinden*](https://github.com/Marcel-B/ChangeMyVoice/blob/main/docs/ui-anbinden.md) in the ChangeMyVoice repository describes it. The gateway checks the key and the origin of the request, which is why it is called from this server rather than from the browser — server to server, so no CORS is needed and the key never leaves the machine.

## Instruments

An instrument is a name for a MIDI output and channel: what the [preview](#preview) routes tracks to and what the exports put them on. Its `kind` is `Synth` (the default) or `DrumMachine`; a drum machine also carries `drums`, the note of each of its drums (`kick`, `snare`, `closedHiHat`, `openHiHat`, `crash`, `clap`, each 0-127, General MIDI when left out), which the drum tracks that play it are generated on. The library, the assignment of tracks to instruments and the [presets](#presets) are the only state the application keeps, in one SQLite file:

| Endpoint | Request | Response |
|---|---|---|
| `GET /api/instruments` | – | `[{ "id": 1, "name": "Mother32", "port": "MIDI4x4 Midi Out 1", "channel": 12, "kind": "Synth", "drums": null }, { "id": 2, "name": "DrumBrute Impact", "port": "MIDI4x4 Midi Out 2", "channel": 8, "kind": "DrumMachine", "drums": { "kick": 36, "snare": 37, "closedHiHat": 44, "openHiHat": 45, "crash": 51, "clap": 39 } }]`, ordered by name |
| `POST /api/instruments` | `{ "name", "port", "channel" }` (channel 1-16), optionally `"kind"` and, for a drum machine, `"drums"` | `201` with the instrument; `400` naming what is wrong; `409` if the name is taken |
| `PUT /api/instruments/{id}` | the same | `200` with the instrument; `404`, `400`, `409` as above |
| `DELETE /api/instruments/{id}` | – | `204`; the tracks that played it lose their assignment |
| `GET /api/instruments/assignments` | – | `{ "Bass": 1, "Vocal 8vb": 2 }` (track name → instrument id) |
| `PUT /api/instruments/assignments/{track}` | `{ "instrumentId": 1 }`, or `null` to take it away | `204`; `404` for an unknown instrument |

The port is the name Web MIDI reports in the browser - on a Mac the CoreMIDI display name, device and port together ("MIDI4x4 Midi Out 1") or just the one name when they are the same ("Scarlett 8i6 USB").

## Presets

A preset is the web form under a name: the server keeps the form as the JSON the interface sent and hands it back unread, so an option added to the form needs nothing on the server. A preset belongs to a user, and its name is unique per user, case-insensitively; there is no login yet, so everything belongs to the one user `local`, which the database creates. A host that adds authentication replaces the `ICurrentUser` service, and the store needs no change.

| Endpoint | Request | Response |
|---|---|---|
| `GET /api/presets` | – | `[{ "id": 1, "name": "Live", "form": { … }, "updatedAt": "2026-09-22T14:05:00.000Z" }]`, ordered by name |
| `PUT /api/presets/{name}` | `{ "form": { … } }` (a JSON object, at most 64 KiB; the name at most 64 characters) | `201` with the preset when it is new, `200` when it replaced one of that name; `400` naming what is wrong |
| `DELETE /api/presets/{name}` | – | `204`; `404` for an unknown name |

Where the file lives is set with `Data:Path` (`Data__Path` in the container); empty means `App_Data/yue-to-logic.db` next to the application, which is what a development run uses. The file and its tables are created on first use, so a server whose data directory is not writable still converts - only the instrument endpoints fail, and the interface says so and carries on without instruments. The schema carries a version, and a file written by an earlier release is upgraded in place the first time it is opened.

## Container and deployment

CI builds a container image with API and web interface and pushes it to the GitHub Container Registry once tests and publish have passed:

| Tag | Meaning |
|---|---|
| `ghcr.io/marcel-b/yue-to-logic-pro:latest` | newest commit on `main` |
| `…:sha-3f2c1ab` | a specific commit |
| `…:1.2.0`, `…:1.2` | a release, created by pushing a tag: `git tag v1.2.0 && git push origin v1.2.0` |

The image listens on port 8080, runs as an unprivileged user and keeps its only state, the [instrument library](#instruments), in the volume `/data` (`Data__Path=/data/yue-to-logic.db`). To try it locally: `docker build -t yue-to-logic . && docker run --rm -p 8080:8080 -v yue-to-logic-data:/data yue-to-logic`, then open <http://localhost:8080>.

### Proxmox container with Docker Compose

1. **Container:** a Debian LXC container. For Docker inside an unprivileged container, enable *Options → Features → nesting* and *keyctl*. Install Docker Engine with the Compose plugin as described at <https://docs.docker.com/engine/install/debian/>.
2. **Files:** copy [`deploy/compose.yml`](deploy/compose.yml) and [`deploy/.env.example`](deploy/.env.example) to e.g. `/opt/yue-to-logic/`, rename `.env.example` to `.env` and adjust it. The instrument library goes into the docker volume `yue-to-logic_data`, which survives updates; `DATA_DIR` puts it into a host directory instead, which must then belong to the app's user (`chown 1654:1654`) and must not be a network share, since SQLite relies on file locks.
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
| `Chords` | The chord symbols played as block chords (root in octave 3, slash bass below), plus the symbol as a text event. With `--chord-pattern` instead `eighths` (the whole chord on every eighth), `offbeat` (short chords on the off-beats only) or `arpeggio` (the chord notes one after another, in eighths, upwards). `--chord-voicing` picks the inversion: `first` and `second` are fixed, `closest` voices every chord where it lies nearest to the one before it, so the track stops jumping an octave at every change. Every voicing keeps its lowest chord tone inside one octave, so the track cannot drift out of its register |
| `Bass` | Only with `--bass`: the bass note of every chord in the register E2–D♯3 (MIDI 40–51, in Logic's naming E1–D♯2), which every bass instrument can play; `--bass-octave -1` goes down to the lowest bass-guitar string. Slash chords such as `C/E` play their bass note. Notes are slightly detached, on-beat notes a little louder. `root-fifth` alternates quarter notes between bass note and the chord's fifth, `octaves` eighth notes with the octave above, `offbeat` plays the off-beat eighths only and `sustained` one long note per chord |
| `Guide` | Only with `--guide-tones`: the third and the seventh of every chord, or its fifth when the chord has no seventh, held as long as the chords keep both notes. These are the two notes that tell a chord apart from its neighbours, which makes the track a pad or a string line while bass and drums carry the rhythm |
| `Vocal 8vb` | Only with `--double-vocal`: the vocal melody again, an octave lower (or wherever `--double-octave` puts it), as a second part for another instrument |
| `Drums` | Only with `--drums`: *four on the floor* plays the kick on every beat, *backbeat* on 1 and 3 with an open hi-hat on the last eighth (4+), *half-time* on 1 only with the snare on 3, *disco* on every beat with an open hi-hat on every off-beat. The others play the snare on 2 and 4, a closed hi-hat in eighth notes (off-beats softer) and a crash cymbal at the start of every section. General MIDI note numbers on channel 10, which Logic's drum kits understand, unless `--drum-note` (web interface: a drum machine [instrument](#instruments)) puts a drum on the note a drum machine listens on. In 3/4 the snare plays on beat 2; 6/8 is counted in dotted quarters |

`--swing` and `--humanize` apply to every track once it has been generated: swing delays the off-beat subdivisions and shortens them by the same amount, so the following note keeps its place, and humanization moves each note a little and varies its velocity. Both change the notes themselves rather than a playback setting, so the MIDI file, the JSON dump and the Logic project carry the same timing. Humanization is seeded, so the same score and options always give the same file.

`--mono` prepares the melodic tracks (and the bass) for monophonic synthesizers, where three things otherwise get in the way: two notes sounding at once, which the synth answers by dropping one; notes that touch, where the envelope is never re-triggered and two notes come out as one long slide; and notes so short that a slow envelope never opens. It keeps one note at a time, leaves a short gap before the next attack and stretches the shortest notes. `--legato` additionally lets every note reach to the one after it, so the track becomes a continuous line of gates. The clean-up runs after the groove, so its guarantees also hold for what is finally written.

The arrangement options are also available in the library (`ConversionOptions.Arrangement`), and the generated tracks appear in the JSON output with `"kind": "Chords"`, `"Bass"`, `"Drums"`, `"GuideTones"` or `"Doubling"`. With `--split-drums` (web interface: *One track per drum*) the kit spreads over the tracks `Kick`, `Snare`, `HiHat` and `Crash` — the same notes, only apart, so that every drum can have its own instrument and its own place in the mix. They all stay on the General MIDI drum channel, and a drum the pattern never plays gets no track. The patterns are written in drums, not in notes: `arrangement.drums.notes` (`kick`, `snare`, `closedHiHat`, `openHiHat`, `crash`, `clap`, each 0-127) says which note each drum is played on, General MIDI when left out, and a split kit sorts by drum, so two drums on one note still land on their own tracks. A chord pattern builds the chord track during arrangement, so the MIDI file and the Logic project play exactly the same notes.

Which tracks a Logic project has comes from the template, not from this tool. The one shipped with it has eleven MIDI tracks — `Vocal`, `Ins`, `Vocal 8vb`, `Chords`, `Bass`, `Guide`, `Drums` and `Kick`, `Snare`, `HiHat`, `Crash` for a split kit — and three audio tracks for the recording and its stems. A voice the template has no track for — a doubling of the instrumental voice (`Ins 8vb`), say — reaches the MIDI file but not the Logic project; a warning (`YTL053`) says so and lists the tracks the template does have. Saving your own template with a track of that name — see *Logic Pro project* below — fills it as well.

## Fitting the tempo

YuE's audio and its symbolic score do not always agree on how long the song is. Where the difference is a fraction of a percent the score is simply a little off, and audio and MIDI drift apart towards the end of the song. `--fit-tempo` stretches the tempo so the score lasts exactly as long as the recording:

```
Info YTL060: Tempo fitted to the audio: 105 → 105.479 BPM, so the score's 354.3 s become the audio's 352.7 s.
```

A large difference means something else. YuE stops generating at a limit — 300 or 360 seconds in the runs this was built against — so the audio can end long before the score does, in one case 37 bars early. Fitting the tempo would then compress the whole song into a length the music never had. Anything more than five percent is therefore reported and left alone:

```
Warning YTL061: The tempo was not fitted: the score lasts 368.1 s but the audio 300.0 s, a difference of
22.7 %. That is more than a drift; the audio was probably cut short, or it belongs to another take.
```

`--fit-tempo` needs the recording to measure and therefore goes together with `--logic <audio.flac>`. The fitted tempo reaches the MIDI file, the JSON dump and the Logic project alike, since it is applied before any of them is written. A count-in is left out of the comparison: the recording holds the music, not the silence in front of it. The library itself never opens a file — the host measures the audio and passes `fitTempo.audioSeconds`; in the web interface the browser reads the 42-byte FLAC header, so nothing has to be uploaded for a MIDI-only conversion.

The web interface offers the same under *Fit the tempo to the audio length*, and `fitTempo.maxDeviation` widens the five percent for a recording you know is right.

## Count-in

`--count-in <n>` puts `n` silent bars in front of the song, so there is a lead-in when playing the parts into hardware or recording along. Everything moves with the music: notes, chords, sections, and every meter and key change — only the signature and key the song opens in stay at bar 1, since they govern the lead-in as well. A click sounds on every beat, the downbeat of each bar harder, as a side stick on the drum track (General MIDI note 37) - or, when the drums play a drum machine with notes of its own (`--drum-note`, a drum machine [instrument](#instruments)), as that machine's clap, since few drum machines have a side stick and whatever sits on note 37 there would count in instead; `arrangement.countIn.note` picks any other note. A score without drums gets a drum track for it, `--count-in-silent` leaves the bars empty.

In a Logic project the audio moves too: the MIDI regions still begin at bar 1 and carry the silent bars inside them, while the recording, which has no count-in of its own, starts where the music does. With `--logic-split-sections` the lead-in becomes a region of its own in front of the first section.

## The input format

YuE2 writes a deliberately small subset of ABC notation. A generic ABC parser would misread it: most importantly, an accidental applies to its note letter **in every octave** until the bar line (after `^F`, `f` is sharp too). The parser in this project follows the YuE2 rules and reports everything outside them as a diagnostic instead of failing, so hand-edited scores still convert. The full rules are described in [YuE2's ABC editing reference](https://github.com/multimodal-art-projection/YuE/blob/main/skills/yue2-music/references/abc-editing.md).

## Project layout

```
src/YueToLogic.Core/    Library: ABC parsing, score model, MIDI rendering
src/YueToLogic.Cli/     Command-line tool (yue2logic)
  Logic/                Logic Pro project writer and the embedded template
src/YueToLogic.Api/     ASP.NET Core API; serves the web frontend under /ui
  ClientApp/            Vue 3 + Vite + TypeScript frontend
  Instruments/          The instrument library in SQLite (the only state the server keeps)
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

- **Several songs of a run** offered for choosing, instead of quietly taking the first one.
- More accompaniment patterns for drums, chords and bass, whenever working with the tool calls for them.
