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
| `--bass-pattern <p>` | Bassrhythmus: `eighths` (Standard), `quarters`, `root-fifth`; schließt `--bass` ein |
| `--bass-octave <n>` | Bass um `n` Oktaven verschieben (−2 bis 2); schließt `--bass` ein |
| `--drums` | Schlagzeugspur hinzufügen (siehe unten) |
| `--drum-pattern <p>` | Groove: `four-on-the-floor` (Standard), `backbeat`; schließt `--drums` ein |
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
| `POST /api/convert/logic` | wie oben, optional `audio` (die `audio.flac`, bis 250 MB) und `name` | ein ZIP mit `<name>.logicx`; Hinweise im Header `X-YueToLogic-Diagnostics`; `422`, wenn das Audio kein FLAC mit 48 kHz ist |
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
    "drums": { "pattern": "Backbeat", "crashOnSections": true }
  }
}
```

```sh
curl -F file=@score.abc -F 'options={"arrangement":{"drums":{}}}' http://localhost:5080/api/convert/midi -o score.mid
```

Clients, die von einem anderen Origin ausgeliefert werden (z. B. eine Electron-Hülle), müssen in `appsettings.json` unter `Cors:AllowedOrigins` eingetragen werden. Die OpenAPI-Beschreibung liegt unter `/api/openapi`.

## Logic-Pro-Projekt (experimentell)

Mit der `audio.flac` von YuE (CLI `--logic`, Weboberfläche: zweite Drop-Zone, dann *Logic-Projekt herunterladen*) entsteht ein komplettes Logic-Pro-Projekt: das Audio auf Spur 1 ab Takt 1, die Spuren Vocal, Ins, Chords, Bass und Drums als MIDI-Regionen, die Abschnitte des Songs als Arrangement-Marker und jeder Akkord auf Logics Akkordspur (der die Session Player folgen können), dazu Tempo, Tonart, alle Taktartwechsel und die Projektlänge aus dem Score. Ohne Audio (CLI `--logic-no-audio`, Weboberfläche: einfach keine `audio.flac` auswählen) entsteht dasselbe Projekt mit leerer Audiospur, auf die sich das FLAC später ziehen lässt; das Audiodatei-Objekt der Vorlage und seine Region werden dabei entfernt, sonst meldet Logic beim Öffnen eine fehlende Datei.

Logics Projektformat ist nicht dokumentiert. Das Projekt entsteht deshalb aus einer von Logic Pro 12.3 gespeicherten Vorlage (`src/YueToLogic.Core/Logic/Template`), in der Noten, Längen, Tempo, Taktart, Marker, Akkorde und Audio ersetzt werden; Akkordregionen und Markernamen über die der Vorlage hinaus werden als neue Objekte angelegt und so registriert, wie Logic es selbst tut. Die in der Vorlage gewählten Instrumente gelten für jedes Projekt. Das Format wurde analysiert und jede Änderung durch Öffnen, Bearbeiten, Speichern und erneutes Öffnen in Logic geprüft. Grenzen: Das Audio muss 48 kHz haben, und die Akkordskalen für die Session Player sind ein Standard je Akkordart. Eine künftige Logic-Version kann eine neu gespeicherte Vorlage erfordern.

Für eigene Klänge legst du eine Vorlage genauso an: eine MIDI-Datei dieses Tools in Logic öffnen (*Ablage → Öffnen*), `audio.flac` auf eine neue Audiospur bei Takt 1 ziehen, Instrumente wählen, mindestens einen Arrangement-Marker und einen Akkord auf der Akkordspur anlegen, als Paket mit ins Projekt kopierten Audiodateien speichern und die Dateien in `Logic/Template` ersetzen (`MetaData.plist` und `ProjectInformation.plist` mit `plutil -convert xml1` umwandeln).

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
| `Chords` | Die Akkordsymbole als Blockakkorde (Grundton in Oktave 3, Slash-Bass darunter), zusätzlich das Symbol als Text-Event |
| `Bass` | Nur mit `--bass`: der Basston jedes Akkords im Register E2–D♯3 (MIDI 40–51, in Logics Benennung E1–D♯2), das jedes Bassinstrument spielen kann; mit `--bass-octave -1` geht es bis zur tiefsten E-Bass-Saite hinunter. Slash-Akkorde wie `C/E` spielen ihren Basston. Die Noten sind leicht gekürzt, Zählzeiten etwas lauter. `root-fifth` wechselt in Vierteln zwischen Basston und Quinte des Akkords |
| `Drums` | Nur mit `--drums`: *Four on the Floor* spielt die Bassdrum auf jedem Schlag, *Backbeat* auf 1 und 3 mit offener Hi-Hat auf der letzten Achtel (4+). Beide spielen die Snare auf 2 und 4, eine geschlossene Hi-Hat in Achteln (Offbeats leiser) und ein Crash-Becken zu Beginn jedes Abschnitts. General-MIDI-Notennummern auf Kanal 10, die Logics Drumkits verstehen. Im 3/4-Takt spielt die Snare auf 2; 6/8 wird in punktierten Vierteln gezählt |

Die Arrangement-Optionen stehen auch in der Bibliothek zur Verfügung (`ConversionOptions.Arrangement`), und die erzeugten Spuren erscheinen in der JSON-Ausgabe mit `"kind": "Bass"` bzw. `"Drums"`.

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

- Auswahl der Instrumente, ohne eine eigene Vorlage anlegen zu müssen.
