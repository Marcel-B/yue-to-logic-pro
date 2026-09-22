# YuE to Logic

[![CI](https://github.com/Marcel-B/yue-to-logic-pro/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcel-B/yue-to-logic-pro/actions/workflows/ci.yml)

[English version](README.md)

Mit [YuE](https://github.com/multimodal-art-projection/YuE) lässt sich Musik per KI erzeugen. Neben dem Audio (`audio.flac`) schreibt ein YuE2-Lauf im Chain-of-Thought-Modus `full` oder `melody` auch eine symbolische Fassung des Songs: `score.abc`. Diese Datei enthält Tempo, Taktart, Tonart, die Songstruktur (Strophe, Refrain, …), eine Gesangs- und eine Instrumentalmelodie sowie die Akkordfolge.

Langfristiges Ziel dieses Projekts ist ein Logic-Pro-Projekt, in dem das generierte Audio als Region liegt und darunter passende MIDI-Spuren. Es soll eine Grundlage zum Analysieren, Bearbeiten oder Erweitern eines Songs sein, keine perfekte Transkription.

**Aktueller Stand:** wandelt `score.abc` in eine MIDI-Datei und zusammen mit dem YuE-Audio in ein Logic-Pro-Projekt um, per Kommandozeile oder über eine Weboberfläche.

## Verwendung

Voraussetzung ist das .NET 10 SDK.

```sh
dotnet run --project src/YueToLogic.Cli -- pfad/zu/score.abc
```

Das schreibt `pfad/zu/score.mid` neben die Eingabedatei und gibt eine Zusammenfassung aus:

```
Eingabe:     /…/score.abc
Tempo:       88 BPM
Taktart:     4/4
Tonart:      C
Länge:       8 Takte, 21,8 s
Abschnitte:  verse (Takt 1), chorus (Takt 5)
Spuren:      Vocal: 56 Noten | Ins: 0 Noten | Akkorde: 8
MIDI:        /…/score.mid
```

| Option | Bedeutung |
|---|---|
| `-o, --output <datei>` | Zu schreibende MIDI-Datei; `.mid` wird angehängt, wenn der Name nicht auf `.mid` oder `.midi` endet (Standard: `<eingabe>.mid`) |
| `--no-chords` | Keine Akkordspur schreiben |
| `--octave <n>` | Beide Melodien um `n` Oktaven verschieben (−4 bis 4) |
| `--vocal-octave <n>`, `--ins-octave <n>` | Nur eine Melodie verschieben; hat Vorrang vor `--octave` |
| `--bass` | Bassspur hinzufügen (siehe unten) |
| `--bass-pattern <p>` | Bassrhythmus: `eighths` (Standard), `quarters`, `root-fifth`, `octaves`, `offbeat`, `sustained`, `walking`; schließt `--bass` ein |
| `--bass-octave <n>` | Bass um `n` Oktaven verschieben (−2 bis 2); schließt `--bass` ein |
| `--drums` | Schlagzeugspur hinzufügen (siehe unten) |
| `--drum-pattern <p>` | Groove: `four-on-the-floor` (Standard), `backbeat`, `half-time`, `disco`, `sixteenth-hats`, `shuffle`; schließt `--drums` ein |
| `--no-crash` | Kein Crash-Becken zu Beginn eines Abschnitts |
| `--split-drums` | Eine Spur je Trommel: `Kick`, `Snare`, `HiHat`, `Crash`; schließt `--drums` ein |
| `--drum-note <trommel>=<note>` | Note, auf der ein Drumcomputer eine Trommel spielt: `kick`, `snare`, `closed-hihat`, `open-hihat`, `crash`, `clap`; die Note als Zahl (`36`) oder wie Logic sie nennt (`C1`); mehrfach möglich, der Rest bleibt General MIDI; schließt `--drums` ein |
| `--chord-pattern <p>` | Akkordbegleitung: `block` (Standard, wie notiert), `eighths`, `sixteenths`, `offbeat`, `arpeggio`, `arpeggio-up-down` |
| `--channel <spur>=<n>` | Fester MIDI-Kanal 1–16 für eine Spur (`Vocal`, `Ins`, `Chords`, `Bass`, `Drums`, `Guide`, `Vocal 8vb`); mehrfach möglich |
| `--program <spur>=<n>` | Programmwechsel 1–128 zu Beginn einer Spur; mehrfach möglich |
| `--chord-voicing <v>` | Lage der Akkorde: `root` (Standard), `closest`, `first`, `second` (siehe unten) |
| `--chord-octave <n>` | Akkordspur um `n` Oktaven verschieben (−2 bis 2) |
| `--guide-tones` | Liegende Spur aus Terz und Septime jedes Akkords hinzufügen (siehe unten) |
| `--guide-octave <n>` | Diese Spur um `n` Oktaven verschieben (−2 bis 2); schließt `--guide-tones` ein |
| `--double-vocal` | Gesangsmelodie eine Oktave tiefer auf einer eigenen Spur verdoppeln |
| `--double-octave <n>` | Oktave dieser Kopie (−2 bis 2, Standard −1); schließt `--double-vocal` ein |
| `--swing <n>` | Swing in Prozent: `0` gerade (Standard), `100` volles Triolenfeeling |
| `--swing-unit <e>` | Welche Unterteilung swingt: `eighths` (Standard), `sixteenths` |
| `--straight-drums` | Schlagzeug gerade lassen, während alles andere swingt |
| `--humanize <n>` | Timing und Anschlag um `n` Prozent streuen (0 Standard; 100 verschiebt eine Note um bis zu 25 ms) |
| `--mono` | Melodien für monophone Synthesizer aufbereiten (siehe unten) |
| `--legato` | Wie `--mono`, zusätzlich reicht jede Note bis zur nächsten |
| `--logic-split-sections` | Im Logic-Projekt eine Region pro Songabschnitt statt einer pro Spur |
| `--count-in <n>` | Stille Takte vor dem Song (0 bis 8), mit Klick auf jedem Schlag (siehe unten) |
| `--count-in-silent` | Kein Klick in diesen Takten; schließt `--count-in 1` ein |
| `--fit-tempo` | Tempo so anpassen, dass der Score so lang ist wie das mit `--logic` angegebene Audio (siehe unten) |
| `--ppq <n>` | MIDI-Auflösung in Ticks pro Viertelnote (Standard 480) |
| `--logic <audio.flac>` | Zusätzlich ein Logic-Pro-Projekt `<ausgabe>.logicx` mit allen Spuren und diesem Audio schreiben (siehe unten) |
| `--logic-no-audio` | Zusätzlich ein Logic-Pro-Projekt ohne Audio schreiben; die Audiospur bleibt leer |
| `--vocals <datei>` | Getrennter Gesang (WAV) für die zweite Audiospur des Logic-Projekts |
| `--vocals-dry <datei>` | Getrennter Gesang ohne Hall (WAV) für die dritte Audiospur |
| `--dump-json <datei>` | Zusätzlich den geparsten Score und alle Meldungen als JSON schreiben; `.json` wird angehängt, wenn es fehlt |
| `-f, --force` | Vorhandene Ausgabedateien überschreiben |
| `-v, --verbose` | Auch Info-Meldungen anzeigen |

Exit-Codes: `0` Erfolg, `1` Score nicht konvertierbar, `2` ungültige Argumente oder Dateifehler. Die CLI antwortet je nach Systemsprache auf Deutsch oder Englisch.

### In Logic Pro

Die MIDI-Datei am besten über *Ablage → Öffnen* öffnen: Logic legt dann ein neues Projekt an, das Tempo, Taktart und Marker ab Takt 1 aus der Datei übernimmt. Zieht man die Datei stattdessen in ein bestehendes Projekt, genau auf Takt 1 ablegen und die Tempo-Übernahme bestätigen; Tempo und Taktart werden relativ zur Ablageposition eingefügt, davor gilt weiter das Projekttempo. Anschließend `audio.flac` aus demselben YuE-Ausgabeordner auf eine neue Audiospur bei Takt 1 ziehen. Da die MIDI-Datei das Tempo aus dem Score mitbringt, laufen beide synchron.

## Weboberfläche

Im Vue-Frontend zieht man eine `score.abc` hinein (oder wählt sie über den Dateidialog), stellt dieselben Parameter wie in der CLI ein und lädt MIDI-Datei und JSON-Dump herunter. Außerdem zeigt es Tempo, Taktart, Tonart, Länge, die Abschnitte des Songs und alle Meldungen.

Statt zweier einzelner Dateien lässt sich auch der **ganze Ausgabeordner eines YuE-Laufs** ablegen oder über *Ordner auswählen* öffnen: Darin werden `score.abc` und `audio.flac` gesucht, auch eine Ebene tiefer in `song1`, `song2` und so weiter. Enthält der Ordner mehrere Songs, wird der erste genommen und die Zahl der übrigen gemeldet.

**Voreinstellungen** über der Parameterliste sichern den ganzen Satz unter einem Namen und holen ihn wieder – für die Kombination aus Mustern, Oktavlagen, Groove und MIDI-Kanälen, mit der du üblicherweise arbeitest. Sie liegen wie die [Instrumente](#instrumente) auf dem Server, jeder Browser bietet also dieselben an, und sie überstehen *Zurücksetzen*, das nur das Formular leert. Voreinstellungen, die ein Browser vor dem Umzug auf den Server gesichert hat, reicht er beim ersten Blick auf einen leeren Server hinüber.

### Vorschau

Jede Konvertierung erscheint als Piano Roll: ein Taktlineal mit den Songabschnitten, die Akkordsymbole und eine Spur je Stimme, zoombar und scrollbar. Das beantwortet die Frage, die bei jedem Parameter wiederkommt – passt die Lage, passt das Muster, sitzen die Akkorde richtig – ohne den Umweg über Logic. Gezeichnet wird auf einem Canvas und nur der sichtbare Ausschnitt, damit auch ein Song mit mehreren tausend Noten flüssig scrollt.

Die Vorschau spielt auch. Jede Spur wird einzeln geroutet, auf einen MIDI-Port mit Kanal oder auf den Ton des Browsers:

- **MIDI** geht an echte Instrumente. Jede Spur hat eigenen Port und Kanal (1–16, wie am Gerät beschriftet), sodass sich ein Rack voller Synthesizer dort ansprechen lässt, wo jedes Gerät hört. Ein *Test*-Knopf je Spur schickt einen einzelnen Ton – der schnellste Weg, ein Verkabelungsproblem von einem Routing-Problem zu unterscheiden –, und *Panik* hebt auf allen Ausgängen jede Taste, falls ein Instrument hängenbleibt. Das Routing wird pro Spurname gemerkt, eine feste Aufstellung stellt man also einmal ein.
- **Browser-Ton** braucht keine Hardware: ein Sägezahn mit Hüllkurve für die melodischen Spuren, gefiltertes Rauschen für das Schlagzeug. Ein grober Ersatz, der für Timing, Swing und Oktavlage reicht.

Web MIDI gibt es nur über HTTPS oder auf localhost und nur in Browsern, die es umsetzen – Chrome ja, Safari nicht; dort bleibt es beim Browser-Ton. Chrome fragt beim ersten Mal nach Erlaubnis, deshalb fordert die Vorschau den Zugriff erst an, wenn du *MIDI-Geräte suchen* drückst.

**Instrumente** ersparen das Eintragen von Port und Kanal ganz. Unter *Instrumente* (das Regler-Symbol in der Kopfzeile, also auch ohne geladenen Score) bekommt jeder Synthesizer einen Namen für seinen Port und Kanal – „WASP Deluxe“ für „Scarlett 8i6 USB“, Kanal 1, „Mother32“ für „MIDI4x4 Midi Out 1“, Kanal 12. Die Routing-Tabelle erhält dann eine Spalte *Instrument*: Eines wählen, und die Spur geht auf dessen Port und Kanal; keines wählen, und die Auswahl von Port und Kanal ist wieder da. Instrumente und Zuordnung der Spuren liegen auf dem Server, jeder Browser sieht also dieselbe Aufstellung; die Ports sind nach Namen gespeichert, sodass ein Instrument, dessen Interface gerade nicht angeschlossen ist, seinen Eintrag behält und bis dahin über den Browser-Ton spielt. Die Wahl geht auch in die Downloads ein: MIDI-Datei und Logic-Projekt legen die Spur auf den Kanal des Instruments, und die Logic-Spur heißt nach beidem (*Bass · Mother32*). Ein Instrument ist entweder ein *Synthesizer* oder ein *Drumcomputer*: Ein Drumcomputer spielt auf seinem einen Kanal mehrere Trommeln, jede auf einer eigenen Note, deshalb bekommt er zusätzlich die Note seiner Kick, Snare, geschlossenen und offenen Hi-Hat, des Crash und des Clap – eingegeben, wie Logic sie nennt (`C1`), oder als Zahl. Die Schlagzeugspuren, die ihn spielen, werden dann auf diesen Noten erzeugt, sodass in der Kick-Region wirklich seine Kick ausgelöst wird: mit einer `Drums`-Spur vom Drumcomputer auf dieser Spur, bei geteiltem Schlagzeug jede der Spuren `Kick`, `Snare`, `HiHat` und `Crash` von ihrem eigenen, und der Vorzähler klickt mit dem Clap. Ohne Drumcomputer bleibt das Schlagzeug wie bisher auf General MIDI. All das braucht Web MIDI und wird deshalb nur unter der Chromium-Engine (Chrome, Edge) angeboten: Safari und Firefox zeigen die Liste nur lesend, mit einem Vermerk dazu, und lassen die Routing-Tabelle bei den von Hand gewählten Ports und Kanälen.

Das Frontend liegt in `src/YueToLogic.Api/ClientApp` und wird von der API unter `/ui` ausgeliefert (`/` leitet dorthin weiter). Zusätzlich zu .NET wird Node.js ab 22.12 benötigt.

**Entwicklung:** Ein Befehl startet alles:

```sh
dotnet run --project src/YueToLogic.Api
```

Der erste Build installiert die npm-Pakete. Beim Start lässt [SpaProxy](https://learn.microsoft.com/aspnet/core/client-side/spa/intro) den Vite-Dev-Server (`npm run dev`) starten, und der Browser öffnet <http://localhost:5080>, das an Vite unter <http://127.0.0.1:5173/ui/> weiterleitet. Änderungen am Vue-Code erscheinen sofort; Vite leitet `/api` an die API zurück.

**Auslieferung:** `dotnet publish` baut das Frontend (`npm ci`, `npm run build`) und legt es als `wwwroot/ui` ab:

```sh
dotnet publish src/YueToLogic.Api -c Release -o publish
dotnet publish/YueToLogic.Api.dll --urls http://localhost:5080
```

Wird das Frontend separat gebaut, z. B. in einer eigenen Docker-Stage, `-p:SkipClientAppBuild=true` übergeben und `ClientApp/dist` nach `wwwroot/ui` kopieren.

### HTTP-API

| Endpunkt | Anfrage | Antwort |
|---|---|---|
| `POST /api/convert` | Multipart-Formular: `file` (der Score), optional `options` (JSON, siehe unten) | `200` mit Score, Meldungen und `midi` (Base64) als JSON; `422` mit Meldungen, wenn Score oder Optionen unbrauchbar sind; `400` bei fehlender Datei oder fehlerhaften Optionen |
| `POST /api/convert/midi` | wie oben | die MIDI-Datei (`audio/midi`) |
| `POST /api/convert/logic` | wie oben, optional `audio` (die `audio.flac`, bis 250 MB), `name`, `splitSections` (`true` für eine Region pro Songabschnitt) und `instruments` (JSON: Spurname → `{ "name", "port", "channel" }`, siehe [Instrumente](#instrumente)) | ein ZIP mit `<name>.logicx`; Hinweise im Header `X-YueToLogic-Diagnostics`; `422`, wenn das Audio kein FLAC mit 48 kHz ist |
| `GET /api/health` | – | `ok` |

`options` ist die JSON-Form von `ConversionOptions`; jedes Feld ist optional:

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

`splitSections` gehört zum Logic-Projekt und nicht zum Score und ist deshalb ein eigenes Formularfeld statt Teil von `options`.

```sh
curl -F file=@score.abc -F 'options={"arrangement":{"drums":{}}}' http://localhost:5080/api/convert/midi -o score.mid
```

Clients, die von einem anderen Origin ausgeliefert werden (z. B. eine Electron-Hülle), müssen in `appsettings.json` unter `Cors:AllowedOrigins` eingetragen werden. Die OpenAPI-Beschreibung liegt unter `/api/openapi`.

## Logic-Pro-Projekt (experimentell)

Mit der `audio.flac` von YuE (CLI `--logic`, Weboberfläche: zweite Drop-Zone, dann *Logic-Projekt herunterladen*) entsteht ein komplettes Logic-Pro-Projekt: das Audio auf Spur 1 ab Takt 1, die Spuren Vocal, Ins, Chords, Bass und Drums als MIDI-Regionen, die Abschnitte des Songs als Arrangement-Marker und jeder Akkord auf Logics Akkordspur (der die Session Player folgen können), dazu Tempo, Tonart, alle Taktartwechsel und die Projektlänge aus dem Score. Die Stems aus der Trennung kommen auf eigene Audiospuren: Die Vorlage hat drei, für die Aufnahme, den getrennten Gesang und den Gesang ohne Hall. Jede Spur wird nach dem benannt, was sie spielt (*Mix*, *Vocals*, *Vocals dry*), und eine Spur ohne Datei verlässt das Projekt, damit Logic nichts vermisst. Ohne Audio (CLI `--logic-no-audio`, Weboberfläche: einfach keine `audio.flac` auswählen) entsteht dasselbe Projekt mit leerer Audiospur, auf die sich das FLAC später ziehen lässt; das Audiodatei-Objekt der Vorlage und seine Region werden dabei entfernt, sonst meldet Logic beim Öffnen eine fehlende Datei.

Mit `--logic-split-sections` (Weboberfläche: *Eine Region pro Abschnitt statt einer pro Spur*) wird jede Spur an den Abschnittsgrenzen geteilt, statt als eine Region durchzulaufen: Die Regionen heißen nach dem Abschnitt, den sie abdecken, und werden nummeriert, wenn ein Name wiederkehrt (`Verse 1`, `Chorus 1`, `Verse 2`, …). So lässt sich ein Abschnitt einzeln kopieren, loopen, stummschalten oder verschieben. Eine Spur, für die der Score nichts hergibt, behält ihre eine leere Region, und eine Note über eine Abschnittsgrenze hinaus behält ihre Länge – die Region wächst mit, statt die Note zu beschneiden. Das Audio bleibt eine Region ab Takt 1.

Logics Projektformat ist nicht dokumentiert. Das Projekt entsteht deshalb aus einer von Logic Pro 12.3 gespeicherten Vorlage (`src/YueToLogic.Core/Logic/Template`), in der Noten, Längen, Tempo, Taktart, Marker, Akkorde und Audio ersetzt werden; Akkordregionen und Markernamen über die der Vorlage hinaus werden als neue Objekte angelegt und so registriert, wie Logic es selbst tut. Die in der Vorlage gewählten Instrumente gelten für jedes Projekt. Das Format wurde analysiert und jede Änderung durch Öffnen, Bearbeiten, Speichern und erneutes Öffnen in Logic geprüft. Grenzen: Das Audio muss 48 kHz haben, und die Akkordskalen für die Session Player sind ein Standard je Akkordart. Eine künftige Logic-Version kann eine neu gespeicherte Vorlage erfordern.

Jede Spur wird nach dem Part benannt, den sie trägt – `Vocal`, `Ins`, `Chords`, `Bass`, `Drums` –, und nicht nach dem Instrument, das die Vorlage zufällig verwendet, denn die Regionen tragen inzwischen die Abschnittsnamen. Logic führt den Spurnamen am Kanalzug, dieser wird also umbenannt; Audiospur und Stereo-Summe behalten ihren. Eine Spur mit [Instrument](#instrumente) heißt nach beidem, *Bass · Mother32*, ihre Noten tragen den Kanal des Instruments, und an Stelle des Software-Instruments der Vorlage bekommt die Spur Logics *External Instrument*, eingeschaltet und auf Ausgang und Kanal des Instruments gestellt, mit ausgeschaltetem MIDI-Input der Spur und ohne die Inserts, die der Sound der Vorlage mitbrachte – das Projekt spielt die Hardware, sobald es aufgeht. Logic findet einen Ausgang über die eindeutige Nummer, die der Mac ihm gegeben hat; geroutet werden kann deshalb nur auf Ausgänge, die der Mac der Vorlage beim Speichern hatte (die Vorlage kennt die Scarlett 8i6, die vier Ausgänge der MIDI4x4 und den Bluetooth-Ausgang CME WIDI). Ein Instrument auf einem anderen Ausgang behält das Software-Instrument, und ein Hinweis (`YTL055`) sagt das. Eine neu gespeicherte Vorlage nimmt neue Interfaces mit.

**Eigene Klänge und weitere Spuren.** Welche Spuren ein Projekt hat, gibt die Vorlage vor – eine dort ergänzte Spur ist überall vorhanden. Konvertiere den Score einmal mit den gewünschten Spuren (`--guide-tones`, `--double-vocal`), öffne die MIDI-Datei in Logic (*Ablage → Öffnen*), das die Spuren danach benennt, und baue die Vorlage daraus: `audio.flac` auf eine neue Audiospur bei Takt 1 ziehen, Instrumente wählen, mindestens einen Arrangement-Marker und einen Akkord auf der Akkordspur anlegen, als Paket mit ins Projekt kopierten Audiodateien speichern und die Dateien in `Logic/Template` ersetzen (`MetaData.plist` und `ProjectInformation.plist` mit `plutil -convert xml1` umwandeln). Eine Spur wird über ihren Namen einer Stimme zugeordnet, die Namen aus der MIDI-Datei also beibehalten. Gewöhnliche Software-Instrument-Spuren verwenden, mit oder ohne gewähltes Instrument: Eine Drum-Machine-Designer-Spur ist ein Aux mit den Klängen auf eigenen Kanalzügen, auf sie lässt sich kein [Instrument](#instrumente) routen (`YTL053` sagt das). Womit die Vorlage klingt, ist gleichgültig: Gesampelte Instrumente und Hall mit Impulsantworten merken sich, wo ihre Dateien lagen, aber jedes erzeugte Projekt wird von diesen Pfaden befreit – Logic findet seine eigenen Inhalte selbst.

## Stems (optional)

Auf Wunsch schickt die Weboberfläche die `audio.flac` an [StemMyWav](https://github.com/Marcel-B/StemMyWav) und bekommt sie in Gesang und Instrumental getrennt zurück. Getrennt wird auf einem Mac mit Metal-GPU; das dauert je nach Länge einige Minuten. Der Ablauf ist deshalb asynchron: Der Auftrag wird angelegt, die Oberfläche fragt alle fünf Sekunden nach dem Stand und meldet sich mit einem Fenster, sobald er fertig ist. Dort wählst du, ob das Logic-Projekt gleich mit den Stems geladen werden soll, ob die Stems verworfen werden oder ob du später über den gewohnten Knopf lädst. Heruntergeladen wird von selbst nichts; die Stems bleiben beim Dienst, bis sie ins Projekt gewandert sind oder du sie löschst. Wer sie einzeln haben will, lädt sie als ZIP. Mit dem Schalter *Hall vom Gesang trennen* kommen `vocals_dry.wav` und `vocals_reverb.wav` dazu.

**Womit getrennt wird.** Der Dienst kennt mehrere Trennmodelle; die Auswahl *Modell* über dem Knopf zeigt, was er anbietet. Die Oberfläche holt die Liste beim Dienst, ein Gateway mit neuen Modellen braucht hier also keine Änderung. Unter der Auswahl steht, was das gewählte Modell tut: welche Stems zurückkommen und wie lange es rechnet, gemessen an der Spieldauer – ein Modell mit `realtimeFactor` 0,3 braucht für vier Minuten Musik rund dreizehn Minuten, eines mit 2,5 etwa anderthalb. Die Voreinstellung des Dienstes ist markiert und vorausgewählt, die zuletzt getroffene Wahl merkt sich der Browser. Ein Modell ohne Gesangs-Stem – ein Instrumental- oder Schlagzeugmodell – sagt das: Seine Stems lassen sich als ZIP laden, die Stem-Spuren des Projekts bleiben aber leer, und der Hall-Schalter, der einen Gesangs-Stem braucht, steht damit nicht zur Verfügung.

Der API-Schlüssel bleibt dabei im Server: Der Browser spricht nur mit dieser Anwendung, die die Anfragen weiterreicht.

| Endpunkt | Anfrage | Antwort |
|---|---|---|
| `GET /api/stems` | – | `{"available":true}`, wenn ein Stem-Dienst eingerichtet ist |
| `GET /api/stems/models` | – | die Trennmodelle des Dienstes, je mit `id`, `name`, `stems`, `speed`, `realtimeFactor` und `isDefault` |
| `POST /api/stems?dereverb=false&model=` | die rohe FLAC als Body (`Content-Type: audio/flac`) | Auftrag mit `id`, `status` und dem `model`, mit dem er läuft |
| `GET /api/stems/{id}` | – | `queued`, `processing`, `completed` oder `failed` samt `lastError` und `model` |
| `GET /api/stems/{id}/result` | – | das ZIP mit den WAV-Stems |
| `DELETE /api/stems/{id}` | – | bestätigt den Import; der Dienst löscht Ergebnis und Auftrag. Bricht einen noch wartenden Auftrag (`queued`) ab; `409`, solange er gerade zum Mac übertragen wird |
| `GET /api/stems/jobs` | – | alle Aufträge, die der Dienst kennt, jüngste zuerst, mit `createdUtc` und `updatedUtc` |

**Warteschlange aufräumen.** Der Dienst nimmt nur wenige wartende Aufträge an und meldet darüber hinaus *Warteschlange voll*, hat aber keine eigene Oberfläche. Das Fenster *Stem-Dienst* (das Wellen-Symbol in der Kopfzeile, nur mit eingerichtetem Stem-Dienst zu sehen) listet, was er kennt, aktualisiert sich alle fünf Sekunden, solange es offen ist, markiert den Auftrag dieses Browser-Tabs und hat je Auftrag einen Knopf: *Abbrechen* für einen wartenden, *Löschen* für einen fertigen oder fehlgeschlagenen; ein Auftrag, der gerade zum Mac übertragen wird, lässt sich erst danach entfernen. Wird dort der Auftrag gelöscht, auf den dieser Tab wartet, lässt auch er ihn los.

Die Stems müssen dafür nicht durch den Browser: Beim Logic-Export genügt das Feld `stemJob` mit der Auftrags-ID, dann holt der Server die WAVs selbst beim Stem-Dienst und legt sie auf die Audiospuren des Projekts. Danach bestätigt er den Import, womit der Dienst seine Dateien löscht. Lässt sich ein Auftrag nicht laden, entsteht das Projekt trotzdem – ohne Stems und mit einer Warnung (`YTL054`).

Eingerichtet wird das über `Stems:BaseUrl` und `Stems:ApiKey` (im Container `Stems__BaseUrl` und `Stems__ApiKey`, siehe [`deploy/.env.example`](deploy/.env.example)). Fehlt eines von beiden, antworten die Endpunkte mit `501` und die Oberfläche zeigt den Bereich gar nicht erst an. Läuft der Gateway im selben Docker-Host, ist `http://stemmywav:8080` die Adresse; dafür muss dieses Compose-Projekt dessen Netz beitreten (in [`deploy/compose.yml`](deploy/compose.yml) auskommentiert vorbereitet).

## Stimme ändern (optional)

Eine fertige Trennung kann jemand anderes singen: Der Gesangs-Stem geht an ChangeMyVoice, das Melodie, Phrasierung und Vortrag behält und ihnen das Timbre einer gespeicherten Stimme gibt. Auch das rechnet auf einem Mac und dauert einige Minuten, es läuft also wie eine Trennung – der Auftrag wird angelegt, die Oberfläche fragt alle fünf Sekunden nach dem Stand und meldet sich mit einem Fenster, sobald er fertig ist. Dort wählst du, ob das Logic-Projekt gleich mit der neuen Stimme geladen werden soll, ob das Ergebnis verworfen wird oder ob du später über den gewohnten Knopf lädst. Heruntergeladen wird von selbst nichts; wer den Gesang einzeln haben will, holt sich die WAV.

Die Reihenfolge ist also: `audio.flac`, *Stems erzeugen*, und sobald die da sind unter *Stimme ändern* eine Stimme wählen und starten. Der Gesang geht dabei nicht durch den Browser – der reicht nur die ID der Trennung und die der Stimme weiter, der Server holt den Gesangs-Stem beim Stem-Dienst und gibt ihn weiter. Wo die Trennung auch den trockenen Gesang erzeugt hat, wird dieser genommen: Das Modell ahmt nach, was es hört, und ein mitgesungener Hall bleibt im Ergebnis.

**Die Sammlung der Modellstimmen.** Eine Modellstimme ist eine Aufnahme einer Stimme, vom Dienst unter einem Namen abgelegt – die Sammlung, aus der eine Umwandlung ihr Timbre wählt, so wie die Instrumentenbibliothek das ist, worauf Spuren geroutet werden. Das Fenster *Modellstimmen* (das Mikrofon in der Kopfzeile, nur mit eingerichtetem Voice-Dienst zu sehen) listet, was der Dienst hat, samt den Eigenschaften der Aufnahme, wie sie hochgeladen wurde und wie der Dienst sie behält, und legt aus Name und Datei eine neue an (WAV, MP3, FLAC, M4A/AAC oder OGG/Opus). Der Dienst behält davon die ersten 25 Sekunden als Mono-PCM mit 44,1 kHz, mehr braucht das Modell nicht; sauberer, trockener Gesang ohne Begleitung führt zum besten Ergebnis. Eine Stimme, auf die noch ein Auftrag wartet, lässt sich nicht löschen – der Dienst lehnt das mit `409` ab.

**Die Aufträge.** Das zweite Mikrofon-Symbol öffnet die Aufträge des Voice-Dienstes: was er kennt, alle fünf Sekunden aktualisiert, solange das Fenster offen ist, der Auftrag dieses Browser-Tabs markiert, und je Auftrag ein Knopf – *Abbrechen* für einen wartenden oder laufenden, *Löschen* für einen fertigen, dessen Ergebnis danach weg ist. Wird dort der Auftrag gelöscht, auf den dieser Tab wartet, lässt auch er ihn los. Ein aufgeräumter Auftrag bleibt beim Dienst als Eintrag erhalten, die Liste wächst also immer weiter: Sie kommt in Seiten zu fünfundzwanzig, *Zurück* und *Weiter* blättern, die Zeile daneben sagt, welcher Teil von wie vielen zu sehen ist, und die Auswahl schränkt auf einen Zustand ein – so findet man, was wartet oder fehlgeschlagen ist. Nicht jedes ChangeMyVoice kennt die Route für alle seine Aufträge; eines ohne sie antwortet mit `404`, was dieser Server als `501` weiterreicht und das Fenster erklärt, statt eine leere Liste zu zeigen.

Der API-Schlüssel bleibt auch hier im Server: Der Browser spricht nur mit dieser Anwendung, die die Anfragen weiterreicht.

| Endpunkt | Anfrage | Antwort |
|---|---|---|
| `GET /api/voice` | – | `{"available":true}`, wenn ein Voice-Dienst eingerichtet ist |
| `GET /api/voice/voices` | – | die Sammlung, je Stimme `id`, `label`, `createdUtc` und die Eigenschaften der abgelegten (`stored`) und der hochgeladenen Aufnahme (`original`) |
| `POST /api/voice/voices` | Formular mit `label` und `file` | `201` mit der Stimme; `400`, wenn eines von beiden fehlt |
| `DELETE /api/voice/voices/{id}` | – | `204`; `409`, solange noch ein Auftrag auf die Stimme wartet |
| `POST /api/voice/jobs` | Formular mit `voiceId` und `stemJob` (die ID einer fertigen Trennung) | `202` mit dem Auftrag; `400` ohne eines von beiden, `501` ohne Stem-Dienst |
| `GET /api/voice/jobs/{id}` | – | `QUEUED`, `RUNNING`, `COMPLETED`, `FAILED` oder `CANCELLED` samt `voiceLabel`, den Zeitpunkten und, im Fehlerfall, `errorCode` und `errorMessage` |
| `GET /api/voice/jobs/{id}/result` | – | die umgewandelte Aufnahme als WAV |
| `DELETE /api/voice/jobs/{id}` | – | bricht einen Auftrag ab oder löscht das Ergebnis eines fertigen; wiederholbar |
| `GET /api/voice/jobs?status=&limit=&offset=` | – | eine Seite der Aufträge, jüngste zuerst: `{ "jobs": [...], "total", "limit", "offset" }`. `limit` reicht von 1 bis 200 (ohne Angabe nimmt der Dienst 50), `status` schränkt auf `QUEUED`, `RUNNING`, `COMPLETED`, `FAILED` oder `CANCELLED` ein; `501` von einem Dienst, der sie nicht auflisten kann |

Auch der geänderte Gesang muss nicht durch den Browser: Beim Logic-Export genügt das Feld `voiceJob` mit der Auftrags-ID, dann holt der Server die WAV selbst und legt sie auf die Vocals-Spur des Projekts – anstelle des getrennten Gesangs, während der trockene, wo eine Trennung ihn erzeugt hat, seine eigene Spur behält. Danach bestätigt er den Import, womit der Dienst das Ergebnis löscht. Zwei Dinge werden geprüft, bevor die Datei ins Paket wandert, und jedes von beiden lässt das Projekt beim getrennten Gesang und meldet eine Warnung (`YTL056`), statt einen sonst fehlerfreien Export scheitern zu lassen: die Prüfsumme, die der Dienst für sein Ergebnis nennt, damit ein abgebrochener Transfer nicht als abgeschnittene Datei im Projekt landet, und die Abtastrate, denn die Audiospuren des Projekts sind auf 48 kHz vorbereitet, während ChangeMyVoice mit der Rate seines Modells rechnet. In beiden Fällen bleibt das Ergebnis beim Dienst, lässt sich also laden und von Hand einsetzen.

Ewig liegt es dort nicht: Eine Stunde nach dem ersten Abruf, spätestens einen Tag nach dem Auftrag, räumt der Dienst es weg. Was bleiben soll, wird also heruntergeladen – entweder ins Logic-Projekt oder als WAV.

Was der Dienst ablehnt, begründet er im Feld `code` seines Problem-Dokuments, und das sagt mehr als der Status: Ein `409` ist eine vergebene Bezeichnung (`DUPLICATE_VOICE_LABEL`), eine Stimme, auf die noch ein Auftrag wartet (`VOICE_IN_USE`), oder ein Ergebnis, das noch nicht fertig ist (`RESULT_NOT_READY`). Aus diesen Codes wird die Meldung, die die Oberfläche zeigt. Der Status bleibt, wie der Dienst ihn gegeben hat, denn er entscheidet, was als Nächstes zu tun ist: Bei `429` (das Ratenlimit dieses Schlüssels) und `503` (`QUEUE_FULL` oder der rechnende Mac ist weg) lohnt ein späterer Versuch, was die Oberfläche auch sagt, während eine Aufnahme, die der Dienst nicht verwenden kann, durch erneutes Senden nicht brauchbar wird.

Eingerichtet wird das über `Voice:BaseUrl` und `Voice:ApiKey` (im Container `Voice__BaseUrl` und `Voice__ApiKey`, siehe [`deploy/.env.example`](deploy/.env.example)). Fehlt eines von beiden, antworten die Endpunkte mit `501` und die Oberfläche zeigt weder den Bereich noch die beiden Fenster und ihre Symbole. Der Schlüssel entsteht am Gateway mit `scripts/neuer-zugang.sh <name> <anfragen pro minute>` und wird in dessen `clients.json` eingetragen, ein Eintrag je Anwendung – nur so lässt sich ein einzelner Zugang entziehen, und nur so zeigen die Protokolle, wer welchen Auftrag ausgelöst hat; [*Eine externe Oberfläche anbinden*](https://github.com/Marcel-B/ChangeMyVoice/blob/main/docs/ui-anbinden.md) im ChangeMyVoice-Repo beschreibt das. Das Gateway prüft Schlüssel und Herkunft der Anfrage; deshalb ruft dieser Server es auf und nicht der Browser – von Server zu Server, ohne CORS, und der Schlüssel verlässt die Maschine nie.

## Instrumente

Ein Instrument ist ein Name für einen MIDI-Ausgang und Kanal: Dorthin routet die [Vorschau](#vorschau) die Spuren, und darauf legen die Exporte sie. Seine Art (`kind`) ist `Synth` (Standard) oder `DrumMachine`; ein Drumcomputer trägt zusätzlich `drums`, die Note jeder seiner Trommeln (`kick`, `snare`, `closedHiHat`, `openHiHat`, `crash`, `clap`, jeweils 0–127, General MIDI, wenn sie fehlen), auf denen die Schlagzeugspuren erzeugt werden, die ihn spielen. Die Liste, die Zuordnung der Spuren und die [Voreinstellungen](#voreinstellungen) sind der einzige Zustand, den die Anwendung hält, in einer SQLite-Datei:

| Endpunkt | Anfrage | Antwort |
|---|---|---|
| `GET /api/instruments` | – | `[{ "id": 1, "name": "Mother32", "port": "MIDI4x4 Midi Out 1", "channel": 12, "kind": "Synth", "drums": null }, { "id": 2, "name": "DrumBrute Impact", "port": "MIDI4x4 Midi Out 2", "channel": 8, "kind": "DrumMachine", "drums": { "kick": 36, "snare": 37, "closedHiHat": 44, "openHiHat": 45, "crash": 51, "clap": 39 } }]`, nach Namen sortiert |
| `POST /api/instruments` | `{ "name", "port", "channel" }` (Kanal 1–16), optional `"kind"` und, für einen Drumcomputer, `"drums"` | `201` mit dem Instrument; `400` mit dem, was fehlt; `409`, wenn der Name vergeben ist |
| `PUT /api/instruments/{id}` | dasselbe | `200` mit dem Instrument; `404`, `400`, `409` wie oben |
| `DELETE /api/instruments/{id}` | – | `204`; Spuren, die es spielten, verlieren die Zuordnung |
| `GET /api/instruments/assignments` | – | `{ "Bass": 1, "Vocal 8vb": 2 }` (Spurname → Instrument-Id) |
| `PUT /api/instruments/assignments/{track}` | `{ "instrumentId": 1 }` oder `null` zum Entfernen | `204`; `404` bei unbekanntem Instrument |

Der Port ist der Name, den Web MIDI im Browser meldet – auf dem Mac der CoreMIDI-Anzeigename, Gerät und Port zusammen („MIDI4x4 Midi Out 1“) oder nur der eine Name, wenn beide gleich sind („Scarlett 8i6 USB“).

## Voreinstellungen

Eine Voreinstellung ist das Webformular unter einem Namen: Der Server bewahrt das Formular als das JSON auf, das die Oberfläche geschickt hat, und gibt es ungelesen zurück; eine neue Option im Formular braucht auf dem Server also nichts. Eine Voreinstellung gehört einem Benutzer, ihr Name ist je Benutzer eindeutig, ohne Rücksicht auf Groß- und Kleinschreibung. Eine Anmeldung gibt es noch nicht, deshalb gehört alles dem einen Benutzer `local`, den die Datenbank anlegt. Ein Host mit Authentifizierung ersetzt den Dienst `ICurrentUser`; die Ablage bleibt, wie sie ist.

| Endpunkt | Anfrage | Antwort |
|---|---|---|
| `GET /api/presets` | – | `[{ "id": 1, "name": "Live", "form": { … }, "updatedAt": "2026-09-22T14:05:00.000Z" }]`, nach Name sortiert |
| `PUT /api/presets/{name}` | `{ "form": { … } }` (ein JSON-Objekt, höchstens 64 KiB; der Name höchstens 64 Zeichen) | `201` mit der Voreinstellung, wenn sie neu ist, `200`, wenn sie eine gleichen Namens ersetzt hat; `400` mit dem Grund |
| `DELETE /api/presets/{name}` | – | `204`; `404` bei unbekanntem Namen |

Wo die Datei liegt, bestimmt `Data:Path` (im Container `Data__Path`); leer heißt `App_Data/yue-to-logic.db` neben der Anwendung, so läuft es in der Entwicklung. Datei und Tabellen entstehen bei der ersten Benutzung, sodass ein Server mit nicht beschreibbarem Datenverzeichnis trotzdem konvertiert – nur die Instrument-Endpunkte schlagen fehl, und die Oberfläche sagt das und arbeitet ohne Instrumente weiter. Das Schema trägt eine Version, und eine Datei aus einer früheren Ausgabe wird beim ersten Öffnen an Ort und Stelle angehoben.

## Container und Deployment

Die CI baut ein Container-Image mit API und Weboberfläche und pusht es in die GitHub Container Registry, sobald Tests und Publish grün sind:

| Tag | Bedeutung |
|---|---|
| `ghcr.io/marcel-b/yue-to-logic-pro:latest` | neuester Commit auf `main` |
| `…:sha-3f2c1ab` | ein bestimmter Commit |
| `…:1.2.0`, `…:1.2` | ein Release, erzeugt durch einen Tag: `git tag v1.2.0 && git push origin v1.2.0` |

Das Image lauscht auf Port 8080, läuft als unprivilegierter Benutzer und hält seinen einzigen Zustand, die [Instrumente](#instrumente), im Volume `/data` (`Data__Path=/data/yue-to-logic.db`). Lokal ausprobieren: `docker build -t yue-to-logic . && docker run --rm -p 8080:8080 -v yue-to-logic-data:/data yue-to-logic`, dann <http://localhost:8080> öffnen.

### Proxmox-Container mit Docker Compose

1. **Container:** ein Debian-LXC-Container. Für Docker in einem unprivilegierten Container unter *Optionen → Features* die Punkte *nesting* und *keyctl* aktivieren. Docker Engine mit Compose-Plugin nach <https://docs.docker.com/engine/install/debian/> installieren.
2. **Dateien:** [`deploy/compose.yml`](deploy/compose.yml) und [`deploy/.env.example`](deploy/.env.example) z. B. nach `/opt/yue-to-logic/` kopieren, `.env.example` in `.env` umbenennen und anpassen. Die Instrumente landen im Docker-Volume `yue-to-logic_data`, das Updates übersteht; `DATA_DIR` legt sie stattdessen in ein Host-Verzeichnis, das dann dem Benutzer der App gehören muss (`chown 1654:1654`) und keine Netzfreigabe sein darf, weil SQLite auf Dateisperren angewiesen ist.
3. **Starten:** `docker compose pull && docker compose up -d`, prüfen mit `curl http://localhost:8080/api/health` (Antwort `ok`).
4. **Nginx Proxy Manager:** *Hosts → Proxy Hosts → Add Proxy Host*
   - *Details:* Domain Names `music.idsrv.info`, Scheme `http`, Forward Hostname/IP = IP des Containers, Forward Port `8080`, *Block Common Exploits* an.
   - *SSL:* Zertifikat auswählen oder anfordern (für einen nur privat erreichbaren Host braucht Let's Encrypt die DNS-Challenge, alternativ ein vorhandenes `*.idsrv.info`-Wildcard-Zertifikat); *Force SSL* und *HTTP/2 Support* aktivieren.
   - NPM setzt die `X-Forwarded-*`-Header selbst. Für den Logic-Export müssen Audio-Uploads von 45–100 MB durchgehen: Antwortet NPM mit `413 Request Entity Too Large`, unter *Advanced* `client_max_body_size 300m;` eintragen.
5. **AdGuard Home:** unter *Filter → DNS-Umschreibungen* `music.idsrv.info` → IP des **Nginx-Proxy-Manager**-Hosts eintragen (nicht die des App-Containers).
6. Sobald <https://music.idsrv.info> funktioniert, in `.env` auf `ALLOWED_HOSTS=music.idsrv.info;localhost` einschränken und `docker compose up -d` erneut ausführen.
7. **Aktualisieren:** `docker compose pull && docker compose up -d`.

`https://music.idsrv.info/` leitet auf die Oberfläche unter `/ui/` weiter. Zeigt ein Browser dort weiterhin eine andere Seite (typischerweise eine aus der Einrichtungsphase des Proxy Hosts zwischengespeicherte), die Website-Daten für `music.idsrv.info` löschen oder ein privates Fenster verwenden.

Der Container läuft mit schreibgeschütztem Dateisystem, ohne Linux-Capabilities und mit `no-new-privileges`. GitHub legt das Image-Paket beim ersten Push eventuell als *privat* an, obwohl das Repository öffentlich ist: Entweder einmalig unter *Packages → yue-to-logic-pro → Package settings* auf öffentlich stellen oder auf dem Host `docker login ghcr.io` mit einem Token mit `read:packages` ausführen.

## Inhalt der MIDI-Datei

Eine Standard-MIDI-Datei vom Typ 1:

| Spur | Inhalt |
|---|---|
| `Conductor` | Tempo, Taktart, Tonart und ein Marker pro Abschnitt (`verse`, `chorus`, …); Logic übernimmt sie in die globalen Spuren |
| `Vocal` | Die Gesangsmelodie |
| `Ins` | Die Instrumentalmelodie |
| `Chords` | Die Akkordsymbole als Blockakkorde (Grundton in Oktave 3, Slash-Bass darunter), zusätzlich das Symbol als Text-Event. Mit `--chord-pattern` stattdessen `eighths` (der ganze Akkord auf jeder Achtel), `offbeat` (kurze Akkorde nur auf den Gegenschlägen) oder `arpeggio` (die Akkordtöne nacheinander in Achteln aufwärts). `--chord-voicing` wählt die Umkehrung: `first` und `second` liegen fest, `closest` legt jeden Akkord dorthin, wo er dem vorigen am nächsten liegt, sodass die Spur nicht mehr bei jedem Wechsel eine Oktave springt. Jede Lage hält ihren tiefsten Akkordton innerhalb einer Oktave, damit die Spur nicht aus ihrem Register wandert |
| `Bass` | Nur mit `--bass`: der Basston jedes Akkords im Register E2–D♯3 (MIDI 40–51, in Logics Benennung E1–D♯2), das jedes Bassinstrument spielen kann; mit `--bass-octave -1` geht es bis zur tiefsten E-Bass-Saite hinunter. Slash-Akkorde wie `C/E` spielen ihren Basston. Die Noten sind leicht gekürzt, Zählzeiten etwas lauter. `root-fifth` wechselt in Vierteln zwischen Basston und Quinte des Akkords, `octaves` in Achteln mit der Oktave darüber, `offbeat` spielt nur die Achtel-Gegenschläge und `sustained` einen langen Ton je Akkord |
| `Guide` | Nur mit `--guide-tones`: Terz und Septime jedes Akkords – bei Dreiklängen die Quinte –, liegend gehalten, solange beide Töne gleich bleiben. Das sind die zwei Töne, die einen Akkord von seinen Nachbarn unterscheiden; die Spur wird damit zur Fläche oder Streicherlinie, während Bass und Schlagzeug den Rhythmus tragen |
| `Vocal 8vb` | Nur mit `--double-vocal`: die Gesangsmelodie noch einmal, eine Oktave tiefer (oder wohin `--double-octave` sie legt), als zweite Stimme für ein weiteres Instrument |
| `Drums` | Nur mit `--drums`: *Four on the Floor* spielt die Bassdrum auf jedem Schlag, *Backbeat* auf 1 und 3 mit offener Hi-Hat auf der letzten Achtel (4+), *Half-Time* nur auf der 1 mit Snare auf 3, *Disco* auf jedem Schlag mit offener Hi-Hat auf allen Gegenschlägen. Die anderen spielen die Snare auf 2 und 4, eine geschlossene Hi-Hat in Achteln (Offbeats leiser) und ein Crash-Becken zu Beginn jedes Abschnitts. General-MIDI-Notennummern auf Kanal 10, die Logics Drumkits verstehen – es sei denn, `--drum-note` (Weboberfläche: ein Drumcomputer als [Instrument](#instrumente)) legt eine Trommel auf die Note, auf der ein Drumcomputer hört. Im 3/4-Takt spielt die Snare auf 2; 6/8 wird in punktierten Vierteln gezählt |

`--swing` und `--humanize` wirken auf jede Spur, sobald sie erzeugt ist: Swing verzögert die Gegenschläge und kürzt sie um denselben Betrag, sodass die folgende Note ihren Platz behält; die Humanisierung verschiebt jede Note ein wenig und streut den Anschlag. Beides verändert die Noten selbst und nicht eine Wiedergabeeinstellung, sodass MIDI-Datei, JSON-Ausgabe und Logic-Projekt dasselbe Timing tragen. Die Humanisierung läuft mit festem Startwert, derselbe Score und dieselben Parameter ergeben also immer dieselbe Datei.

`--mono` bereitet die Melodiespuren – und den Bass – für monophone Synthesizer auf. Drei Dinge stehen dem sonst im Weg: zwei gleichzeitig klingende Töne, von denen der Synth einen verwirft; sich berührende Noten, bei denen die Hüllkurve nie neu ausgelöst wird und zwei Töne als ein langes Gleiten herauskommen; und Noten, die so kurz sind, dass eine langsame Hüllkurve gar nicht öffnet. Die Aufbereitung lässt immer nur einen Ton klingen, hält vor dem nächsten Anschlag eine kurze Lücke und dehnt die kürzesten Noten. `--legato` lässt zusätzlich jede Note bis zur nächsten reichen, sodass die Spur zu einer durchgehenden Folge von Gates wird. Sie läuft nach dem Groove, damit ihre Zusagen auch für das gelten, was am Ende geschrieben wird.

Die Arrangement-Optionen stehen auch in der Bibliothek zur Verfügung (`ConversionOptions.Arrangement`), und die erzeugten Spuren erscheinen in der JSON-Ausgabe mit `"kind": "Chords"`, `"Bass"`, `"Drums"`, `"GuideTones"` bzw. `"Doubling"`. Mit `--split-drums` (Weboberfläche: *Eine Spur je Trommel*) verteilt sich das Schlagzeug auf die Spuren `Kick`, `Snare`, `HiHat` und `Crash` – dieselben Noten, nur getrennt, damit jede Trommel ihr eigenes Instrument und ihren eigenen Platz in der Mischung bekommt. Alle bleiben auf dem General-MIDI-Schlagzeugkanal, und eine Trommel, die das Muster nicht spielt, bekommt keine Spur. Die Muster sind in Trommeln geschrieben, nicht in Noten: `arrangement.drums.notes` (`kick`, `snare`, `closedHiHat`, `openHiHat`, `crash`, `clap`, jeweils 0–127) sagt, auf welcher Note jede Trommel gespielt wird, General MIDI, wenn es fehlt, und ein geteiltes Schlagzeug sortiert nach Trommel, sodass zwei Trommeln auf einer Note trotzdem auf ihren eigenen Spuren landen. Ein Akkordmuster erzeugt die Akkordspur schon im Arrangement, sodass MIDI-Datei und Logic-Projekt dieselben Noten spielen.

Welche Spuren ein Logic-Projekt hat, gibt die Vorlage vor und nicht dieses Werkzeug. Die mitgelieferte hat elf MIDI-Spuren – `Vocal`, `Ins`, `Vocal 8vb`, `Chords`, `Bass`, `Guide`, `Drums` sowie `Kick`, `Snare`, `HiHat` und `Crash` für ein geteiltes Schlagzeug – und drei Audiospuren für die Aufnahme und ihre Stems. Eine Stimme, für die die Vorlage keine Spur hat – etwa eine Dopplung der Instrumentalstimme (`Ins 8vb`) –, landet in der MIDI-Datei, aber nicht im Logic-Projekt; eine Warnung (`YTL053`) weist darauf hin und nennt die Spuren, die die Vorlage hat. Eine eigene Vorlage mit einer passend benannten Spur – siehe *Logic-Pro-Projekt* weiter unten – füllt auch diese.

## Tempo anpassen

YuEs Audio und sein symbolischer Score sind sich nicht immer einig, wie lang der Song ist. Wo der Unterschied Bruchteile eines Prozents beträgt, liegt der Score schlicht ein wenig daneben, und Audio und MIDI laufen gegen Songende auseinander. `--fit-tempo` dehnt das Tempo so, dass der Score genau so lang wird wie die Aufnahme:

```
Info YTL060: Tempo fitted to the audio: 105 → 105.479 BPM, so the score's 354.3 s become the audio's 352.7 s.
```

Ein großer Unterschied bedeutet etwas anderes. YuE bricht die Erzeugung an einer Grenze ab – in den Läufen, gegen die das entwickelt wurde, bei 300 oder 360 Sekunden –, das Audio kann also lange vor dem Score enden, in einem Fall 37 Takte früher. Das Tempo anzupassen würde den ganzen Song in eine Länge pressen, die die Musik nie hatte. Mehr als fünf Prozent werden deshalb gemeldet statt angewandt:

```
Warnung YTL061: The tempo was not fitted: the score lasts 368.1 s but the audio 300.0 s, a difference of
22.7 %. That is more than a drift; the audio was probably cut short, or it belongs to another take.
```

`--fit-tempo` braucht die Aufnahme als Maß und gehört deshalb mit `--logic <audio.flac>` zusammen. Das angepasste Tempo erreicht MIDI-Datei, JSON-Ausgabe und Logic-Projekt gleichermaßen, weil es vor allen dreien angewandt wird. Ein Vorzähler bleibt beim Vergleich außen vor: Die Aufnahme enthält die Musik, nicht die Stille davor. Die Bibliothek selbst öffnet keine Datei – der Host misst das Audio und übergibt `fitTempo.audioSeconds`; in der Weboberfläche liest der Browser die 42 Byte des FLAC-Kopfes, für eine reine MIDI-Konvertierung muss also nichts hochgeladen werden.

Die Weboberfläche bietet dasselbe unter *Tempo an die Audiolänge anpassen* an, und `fitTempo.maxDeviation` weitet die fünf Prozent für eine Aufnahme, von der du weißt, dass sie stimmt.

## Vorzähler

`--count-in <n>` setzt `n` stille Takte vor den Song, damit beim Einspielen in Hardware oder beim Mitschneiden ein Vorlauf da ist. Alles wandert mit der Musik: Noten, Akkorde, Abschnitte sowie jeder Takt- und Tonartwechsel – nur die Taktart und die Tonart, in denen der Song beginnt, bleiben bei Takt 1, denn sie gelten auch für den Vorlauf. Auf jedem Schlag klingt ein Klick, der erste Schlag jedes Takts lauter, als Side Stick auf der Schlagzeugspur (General-MIDI-Note 37) – oder, wenn das Schlagzeug einen Drumcomputer mit eigenen Noten spielt (`--drum-note`, ein Drumcomputer als [Instrument](#instrumente)), als dessen Clap, denn kaum ein Drumcomputer hat einen Side Stick, und was dort auf Note 37 liegt, würde sonst vorzählen; `arrangement.countIn.note` wählt jede andere Note. Ein Score ohne Schlagzeug bekommt dafür eine Schlagzeugspur, `--count-in-silent` lässt die Takte leer.

Im Logic-Projekt wandert auch das Audio: Die MIDI-Regionen beginnen weiterhin bei Takt 1 und tragen die stillen Takte in sich, während die Aufnahme, die keinen eigenen Vorzähler hat, dort beginnt, wo die Musik einsetzt. Mit `--logic-split-sections` wird der Vorlauf zu einer eigenen Region vor dem ersten Abschnitt.

## Das Eingabeformat

YuE2 schreibt eine bewusst kleine Teilmenge der ABC-Notation. Ein allgemeiner ABC-Parser würde sie falsch lesen: Vor allem gilt ein Vorzeichen für seinen Notenbuchstaben **in allen Oktaven** bis zum Taktstrich (nach `^F` ist auch `f` erhöht). Der Parser in diesem Projekt folgt den YuE2-Regeln und meldet alles, was davon abweicht, als Diagnose, statt abzubrechen – auch von Hand bearbeitete Scores lassen sich also konvertieren. Die vollständigen Regeln stehen in der [ABC-Referenz von YuE2](https://github.com/multimodal-art-projection/YuE/blob/main/skills/yue2-music/references/abc-editing.md).

## Projektaufbau

```
src/YueToLogic.Core/    Bibliothek: ABC-Parser, Score-Modell, MIDI-Erzeugung
src/YueToLogic.Cli/     Kommandozeilenwerkzeug (yue2logic)
  Logic/                Logic-Pro-Projekt-Writer und eingebettete Vorlage
src/YueToLogic.Api/     ASP.NET-Core-API; liefert das Web-Frontend unter /ui aus
  ClientApp/            Vue-3-Frontend mit Vite und TypeScript
  Instruments/          Die Instrumente in SQLite (der einzige Zustand des Servers)
deploy/                 Docker-Compose-Setup für den Server
Dockerfile              Container-Image (API + Frontend)
tests/                  xUnit-Tests
samples/score.abc       Offizielles YuE2-Beispiel
```

`YueToLogic.Core` hat keine Abhängigkeiten zu Konsole oder Dateisystem, damit die Bibliothek später aus einem Webservice, einem Electron/Vue-Frontend oder einer macOS-App genutzt werden kann:

- Eingabe ist ein `string` oder `Stream`, Ausgabe ein `byte[]` oder ein vom Aufrufer gestellter `Stream`.
- Die Wertebereiche der Optionen prüft `ConversionOptionsValidator`, damit jede Anwendung dieselben Werte akzeptiert.
- `services.AddYueToLogic()` registriert die zustandslosen Dienste `IScoreConverter`, `IAbcScoreParser`, `IScoreArranger` und `IMidiRenderer` für Dependency Injection.
- `ConversionResult` und das Modell `ScoreDocument` lassen sich über den quellgenerierten `YueToLogicJsonContext` als JSON serialisieren, sodass ein Frontend den Score anzeigen kann, ohne MIDI zu parsen.
- Probleme werden als `Diagnostic`-Datensätze mit stabilen Codes (`YTL0xx`) zurückgegeben, nicht geloggt oder als Exception geworfen.

```csharp
var result = new ScoreConverter().Convert(abcText);
if (result.Success)
{
    File.WriteAllBytes("song.mid", result.Midi!);
}
```

## Entwicklung

```sh
dotnet build
dotnet test
dotnet publish src/YueToLogic.Api -c Release -o publish   # inkl. Typprüfung und Bündelung des Frontends
```

Die GitHub Action in `.github/workflows/ci.yml` führt dieselben Schritte bei jedem Push und Pull Request aus.

## Nächste Schritte

- **Mehrere Songs eines Laufs** zur Auswahl stellen, statt stillschweigend den ersten zu nehmen.
- Weitere Begleitmuster für Schlagzeug, Akkorde und Bass, sobald sich beim Arbeiten Bedarf zeigt.
