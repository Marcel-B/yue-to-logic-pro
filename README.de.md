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
| `--dump-json <datei>` | Zusätzlich den geparsten Score und alle Meldungen als JSON schreiben; `.json` wird angehängt, wenn es fehlt |
| `-f, --force` | Vorhandene Ausgabedateien überschreiben |
| `-v, --verbose` | Auch Info-Meldungen anzeigen |

Exit-Codes: `0` Erfolg, `1` Score nicht konvertierbar, `2` ungültige Argumente oder Dateifehler. Die CLI antwortet je nach Systemsprache auf Deutsch oder Englisch.

### In Logic Pro

Die MIDI-Datei am besten über *Ablage → Öffnen* öffnen: Logic legt dann ein neues Projekt an, das Tempo, Taktart und Marker ab Takt 1 aus der Datei übernimmt. Zieht man die Datei stattdessen in ein bestehendes Projekt, genau auf Takt 1 ablegen und die Tempo-Übernahme bestätigen; Tempo und Taktart werden relativ zur Ablageposition eingefügt, davor gilt weiter das Projekttempo. Anschließend `audio.flac` aus demselben YuE-Ausgabeordner auf eine neue Audiospur bei Takt 1 ziehen. Da die MIDI-Datei das Tempo aus dem Score mitbringt, laufen beide synchron.

## Weboberfläche

Im Vue-Frontend zieht man eine `score.abc` hinein (oder wählt sie über den Dateidialog), stellt dieselben Parameter wie in der CLI ein und lädt MIDI-Datei und JSON-Dump herunter. Außerdem zeigt es Tempo, Taktart, Tonart, Länge, die Abschnitte des Songs und alle Meldungen.

Statt zweier einzelner Dateien lässt sich auch der **ganze Ausgabeordner eines YuE-Laufs** ablegen oder über *Ordner auswählen* öffnen: Darin werden `score.abc` und `audio.flac` gesucht, auch eine Ebene tiefer in `song1`, `song2` und so weiter. Enthält der Ordner mehrere Songs, wird der erste genommen und die Zahl der übrigen gemeldet.

**Voreinstellungen** über der Parameterliste sichern den ganzen Satz unter einem Namen und holen ihn wieder – für die Kombination aus Mustern, Oktavlagen, Groove und MIDI-Kanälen, mit der du üblicherweise arbeitest. Sie liegen im Browser und überstehen *Zurücksetzen*, das nur das Formular leert.

### Vorschau

Jede Konvertierung erscheint als Piano Roll: ein Taktlineal mit den Songabschnitten, die Akkordsymbole und eine Spur je Stimme, zoombar und scrollbar. Das beantwortet die Frage, die bei jedem Parameter wiederkommt – passt die Lage, passt das Muster, sitzen die Akkorde richtig – ohne den Umweg über Logic. Gezeichnet wird auf einem Canvas und nur der sichtbare Ausschnitt, damit auch ein Song mit mehreren tausend Noten flüssig scrollt.

Die Vorschau spielt auch. Jede Spur wird einzeln geroutet, auf einen MIDI-Port mit Kanal oder auf den Ton des Browsers:

- **MIDI** geht an echte Instrumente. Jede Spur hat eigenen Port und Kanal (1–16, wie am Gerät beschriftet), sodass sich ein Rack voller Synthesizer dort ansprechen lässt, wo jedes Gerät hört. Ein *Test*-Knopf je Spur schickt einen einzelnen Ton – der schnellste Weg, ein Verkabelungsproblem von einem Routing-Problem zu unterscheiden –, und *Panik* hebt auf allen Ausgängen jede Taste, falls ein Instrument hängenbleibt. Das Routing wird pro Spurname gemerkt, eine feste Aufstellung stellt man also einmal ein.
- **Browser-Ton** braucht keine Hardware: ein Sägezahn mit Hüllkurve für die melodischen Spuren, gefiltertes Rauschen für das Schlagzeug. Ein grober Ersatz, der für Timing, Swing und Oktavlage reicht.

Web MIDI gibt es nur über HTTPS oder auf localhost und nur in Browsern, die es umsetzen – Chrome ja, Safari nicht; dort bleibt es beim Browser-Ton. Chrome fragt beim ersten Mal nach Erlaubnis, deshalb fordert die Vorschau den Zugriff erst an, wenn du *MIDI-Geräte suchen* drückst.

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
| `POST /api/convert/logic` | wie oben, optional `audio` (die `audio.flac`, bis 250 MB), `name` und `splitSections` (`true` für eine Region pro Songabschnitt) | ein ZIP mit `<name>.logicx`; Hinweise im Header `X-YueToLogic-Diagnostics`; `422`, wenn das Audio kein FLAC mit 48 kHz ist |
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

Mit der `audio.flac` von YuE (CLI `--logic`, Weboberfläche: zweite Drop-Zone, dann *Logic-Projekt herunterladen*) entsteht ein komplettes Logic-Pro-Projekt: das Audio auf Spur 1 ab Takt 1, die Spuren Vocal, Ins, Chords, Bass und Drums als MIDI-Regionen, die Abschnitte des Songs als Arrangement-Marker und jeder Akkord auf Logics Akkordspur (der die Session Player folgen können), dazu Tempo, Tonart, alle Taktartwechsel und die Projektlänge aus dem Score. Ohne Audio (CLI `--logic-no-audio`, Weboberfläche: einfach keine `audio.flac` auswählen) entsteht dasselbe Projekt mit leerer Audiospur, auf die sich das FLAC später ziehen lässt; das Audiodatei-Objekt der Vorlage und seine Region werden dabei entfernt, sonst meldet Logic beim Öffnen eine fehlende Datei.

Mit `--logic-split-sections` (Weboberfläche: *Eine Region pro Abschnitt statt einer pro Spur*) wird jede Spur an den Abschnittsgrenzen geteilt, statt als eine Region durchzulaufen: Die Regionen heißen nach dem Abschnitt, den sie abdecken, und werden nummeriert, wenn ein Name wiederkehrt (`Verse 1`, `Chorus 1`, `Verse 2`, …). So lässt sich ein Abschnitt einzeln kopieren, loopen, stummschalten oder verschieben. Eine Spur, für die der Score nichts hergibt, behält ihre eine leere Region, und eine Note über eine Abschnittsgrenze hinaus behält ihre Länge – die Region wächst mit, statt die Note zu beschneiden. Das Audio bleibt eine Region ab Takt 1.

Logics Projektformat ist nicht dokumentiert. Das Projekt entsteht deshalb aus einer von Logic Pro 12.3 gespeicherten Vorlage (`src/YueToLogic.Core/Logic/Template`), in der Noten, Längen, Tempo, Taktart, Marker, Akkorde und Audio ersetzt werden; Akkordregionen und Markernamen über die der Vorlage hinaus werden als neue Objekte angelegt und so registriert, wie Logic es selbst tut. Die in der Vorlage gewählten Instrumente gelten für jedes Projekt. Das Format wurde analysiert und jede Änderung durch Öffnen, Bearbeiten, Speichern und erneutes Öffnen in Logic geprüft. Grenzen: Das Audio muss 48 kHz haben, und die Akkordskalen für die Session Player sind ein Standard je Akkordart. Eine künftige Logic-Version kann eine neu gespeicherte Vorlage erfordern.

Jede Spur wird nach dem Part benannt, den sie trägt – `Vocal`, `Ins`, `Chords`, `Bass`, `Drums` –, und nicht nach dem Instrument, das die Vorlage zufällig verwendet, denn die Regionen tragen inzwischen die Abschnittsnamen. Logic führt den Spurnamen am Kanalzug, dieser wird also umbenannt; Audiospur und Stereo-Summe behalten ihren.

**Eigene Klänge und weitere Spuren.** Welche Spuren ein Projekt hat, gibt die Vorlage vor – eine dort ergänzte Spur ist überall vorhanden. Konvertiere den Score einmal mit den gewünschten Spuren (`--guide-tones`, `--double-vocal`), öffne die MIDI-Datei in Logic (*Ablage → Öffnen*), das die Spuren danach benennt, und baue die Vorlage daraus: `audio.flac` auf eine neue Audiospur bei Takt 1 ziehen, Instrumente wählen, mindestens einen Arrangement-Marker und einen Akkord auf der Akkordspur anlegen, als Paket mit ins Projekt kopierten Audiodateien speichern und die Dateien in `Logic/Template` ersetzen (`MetaData.plist` und `ProjectInformation.plist` mit `plutil -convert xml1` umwandeln). Eine Spur wird über ihren Namen einer Stimme zugeordnet, die Namen aus der MIDI-Datei also beibehalten.

## Stems (optional)

Auf Wunsch schickt die Weboberfläche die `audio.flac` an [StemMyWav](https://github.com/Marcel-B/StemMyWav) und bekommt sie in Gesang und Instrumental getrennt zurück. Getrennt wird auf einem Mac mit Metal-GPU; das dauert je nach Länge einige Minuten. Der Ablauf ist deshalb asynchron: Der Auftrag wird angelegt, die Oberfläche fragt alle fünf Sekunden nach dem Stand, lädt am Ende das ZIP mit `vocals.wav` und `instrumental.wav` herunter und bestätigt den Import, woraufhin der Dienst seine Dateien sofort löscht. Mit dem Schalter *Hall vom Gesang trennen* kommen `vocals_dry.wav` und `vocals_reverb.wav` dazu.

Der API-Schlüssel bleibt dabei im Server: Der Browser spricht nur mit dieser Anwendung, die die Anfragen weiterreicht.

| Endpunkt | Anfrage | Antwort |
|---|---|---|
| `GET /api/stems` | – | `{"available":true}`, wenn ein Stem-Dienst eingerichtet ist |
| `POST /api/stems?dereverb=false` | die rohe FLAC als Body (`Content-Type: audio/flac`) | Auftrag mit `id` und `status` |
| `GET /api/stems/{id}` | – | `queued`, `processing`, `completed` oder `failed` samt `lastError` |
| `GET /api/stems/{id}/result` | – | das ZIP mit den WAV-Stems |
| `DELETE /api/stems/{id}` | – | bestätigt den Import; der Dienst löscht Ergebnis und Auftrag |

Eingerichtet wird das über `Stems:BaseUrl` und `Stems:ApiKey` (im Container `Stems__BaseUrl` und `Stems__ApiKey`, siehe [`deploy/.env.example`](deploy/.env.example)). Fehlt eines von beiden, antworten die Endpunkte mit `501` und die Oberfläche zeigt den Bereich gar nicht erst an. Läuft der Gateway im selben Docker-Host, ist `http://stemmywav:8080` die Adresse; dafür muss dieses Compose-Projekt dessen Netz beitreten (in [`deploy/compose.yml`](deploy/compose.yml) auskommentiert vorbereitet).

## Container und Deployment

Die CI baut ein Container-Image mit API und Weboberfläche und pusht es in die GitHub Container Registry, sobald Tests und Publish grün sind:

| Tag | Bedeutung |
|---|---|
| `ghcr.io/marcel-b/yue-to-logic-pro:latest` | neuester Commit auf `main` |
| `…:sha-3f2c1ab` | ein bestimmter Commit |
| `…:1.2.0`, `…:1.2` | ein Release, erzeugt durch einen Tag: `git tag v1.2.0 && git push origin v1.2.0` |

Das Image lauscht auf Port 8080, läuft als unprivilegierter Benutzer und speichert keinen Zustand. Lokal ausprobieren: `docker build -t yue-to-logic . && docker run --rm -p 8080:8080 yue-to-logic`, dann <http://localhost:8080> öffnen.

### Proxmox-Container mit Docker Compose

1. **Container:** ein Debian-LXC-Container. Für Docker in einem unprivilegierten Container unter *Optionen → Features* die Punkte *nesting* und *keyctl* aktivieren. Docker Engine mit Compose-Plugin nach <https://docs.docker.com/engine/install/debian/> installieren.
2. **Dateien:** [`deploy/compose.yml`](deploy/compose.yml) und [`deploy/.env.example`](deploy/.env.example) z. B. nach `/opt/yue-to-logic/` kopieren, `.env.example` in `.env` umbenennen und anpassen.
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
| `Drums` | Nur mit `--drums`: *Four on the Floor* spielt die Bassdrum auf jedem Schlag, *Backbeat* auf 1 und 3 mit offener Hi-Hat auf der letzten Achtel (4+), *Half-Time* nur auf der 1 mit Snare auf 3, *Disco* auf jedem Schlag mit offener Hi-Hat auf allen Gegenschlägen. Die anderen spielen die Snare auf 2 und 4, eine geschlossene Hi-Hat in Achteln (Offbeats leiser) und ein Crash-Becken zu Beginn jedes Abschnitts. General-MIDI-Notennummern auf Kanal 10, die Logics Drumkits verstehen. Im 3/4-Takt spielt die Snare auf 2; 6/8 wird in punktierten Vierteln gezählt |

`--swing` und `--humanize` wirken auf jede Spur, sobald sie erzeugt ist: Swing verzögert die Gegenschläge und kürzt sie um denselben Betrag, sodass die folgende Note ihren Platz behält; die Humanisierung verschiebt jede Note ein wenig und streut den Anschlag. Beides verändert die Noten selbst und nicht eine Wiedergabeeinstellung, sodass MIDI-Datei, JSON-Ausgabe und Logic-Projekt dasselbe Timing tragen. Die Humanisierung läuft mit festem Startwert, derselbe Score und dieselben Parameter ergeben also immer dieselbe Datei.

`--mono` bereitet die Melodiespuren – und den Bass – für monophone Synthesizer auf. Drei Dinge stehen dem sonst im Weg: zwei gleichzeitig klingende Töne, von denen der Synth einen verwirft; sich berührende Noten, bei denen die Hüllkurve nie neu ausgelöst wird und zwei Töne als ein langes Gleiten herauskommen; und Noten, die so kurz sind, dass eine langsame Hüllkurve gar nicht öffnet. Die Aufbereitung lässt immer nur einen Ton klingen, hält vor dem nächsten Anschlag eine kurze Lücke und dehnt die kürzesten Noten. `--legato` lässt zusätzlich jede Note bis zur nächsten reichen, sodass die Spur zu einer durchgehenden Folge von Gates wird. Sie läuft nach dem Groove, damit ihre Zusagen auch für das gelten, was am Ende geschrieben wird.

Die Arrangement-Optionen stehen auch in der Bibliothek zur Verfügung (`ConversionOptions.Arrangement`), und die erzeugten Spuren erscheinen in der JSON-Ausgabe mit `"kind": "Chords"`, `"Bass"`, `"Drums"`, `"GuideTones"` bzw. `"Doubling"`. Mit `--split-drums` (Weboberfläche: *Eine Spur je Trommel*) verteilt sich das Schlagzeug auf die Spuren `Kick`, `Snare`, `HiHat` und `Crash` – dieselben Noten, nur getrennt, damit jede Trommel ihr eigenes Instrument und ihren eigenen Platz in der Mischung bekommt. Alle bleiben auf dem General-MIDI-Schlagzeugkanal, und eine Trommel, die das Muster nicht spielt, bekommt keine Spur. Ein Akkordmuster erzeugt die Akkordspur schon im Arrangement, sodass MIDI-Datei und Logic-Projekt dieselben Noten spielen.

Welche Spuren ein Logic-Projekt hat, gibt die Vorlage vor und nicht dieses Werkzeug; die mitgelieferte hat sieben: `Vocal`, `Ins`, `Chords`, `Bass`, `Drums`, `Guide` und `Vocal 8vb`. Eine Stimme, für die die Vorlage keine Spur hat – etwa eine Dopplung der Instrumentalstimme (`Ins 8vb`) –, landet in der MIDI-Datei, aber nicht im Logic-Projekt; eine Warnung (`YTL053`) weist darauf hin und nennt die Spuren, die die Vorlage hat. Eine eigene Vorlage mit einer passend benannten Spur – siehe *Logic-Pro-Projekt* weiter unten – füllt auch diese.

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

`--count-in <n>` setzt `n` stille Takte vor den Song, damit beim Einspielen in Hardware oder beim Mitschneiden ein Vorlauf da ist. Alles wandert mit der Musik: Noten, Akkorde, Abschnitte sowie jeder Takt- und Tonartwechsel – nur die Taktart und die Tonart, in denen der Song beginnt, bleiben bei Takt 1, denn sie gelten auch für den Vorlauf. Auf jedem Schlag klingt ein Klick, der erste Schlag jedes Takts lauter, als Side Stick auf der Schlagzeugspur (General-MIDI-Note 37); ein Score ohne Schlagzeug bekommt dafür eine Schlagzeugspur, `--count-in-silent` lässt die Takte leer.

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

- **Stems ins Logic-Projekt**, statt sie nur herunterzuladen: Gesang und Instrumental als eigene Audiospuren neben dem Mix. Dafür braucht es eine Vorlage mit mehreren Audiospuren und das Klonen der Audio-Objekte im Projektformat.
- **Mehrere Songs eines Laufs** zur Auswahl stellen, statt stillschweigend den ersten zu nehmen.
- Weitere Begleitmuster für Schlagzeug, Akkorde und Bass, sobald sich beim Arbeiten Bedarf zeigt.
