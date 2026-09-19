# YuE to Logic

[![CI](https://github.com/Marcel-B/yue-to-logic-pro/actions/workflows/ci.yml/badge.svg)](https://github.com/Marcel-B/yue-to-logic-pro/actions/workflows/ci.yml)

[English version](README.md)

Mit [YuE](https://github.com/multimodal-art-projection/YuE) lässt sich Musik per KI erzeugen. Neben dem Audio (`audio.flac`) schreibt ein YuE2-Lauf im Chain-of-Thought-Modus `full` oder `melody` auch eine symbolische Fassung des Songs: `score.abc`. Diese Datei enthält Tempo, Taktart, Tonart, die Songstruktur (Strophe, Refrain, …), eine Gesangs- und eine Instrumentalmelodie sowie die Akkordfolge.

Langfristiges Ziel dieses Projekts ist ein Logic-Pro-Projekt, in dem das generierte Audio als Region liegt und darunter passende MIDI-Spuren. Es soll eine Grundlage zum Analysieren, Bearbeiten oder Erweitern eines Songs sein, keine perfekte Transkription.

**Aktueller Stand:** ein Prototyp, der `score.abc` in eine MIDI-Datei umwandelt, die sich in Logic Pro öffnen lässt.

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
| `--ppq <n>` | MIDI-Auflösung in Ticks pro Viertelnote (Standard 480) |
| `--dump-json <datei>` | Zusätzlich den geparsten Score und alle Meldungen als JSON schreiben; `.json` wird angehängt, wenn es fehlt |
| `-f, --force` | Vorhandene Ausgabedateien überschreiben |
| `-v, --verbose` | Auch Info-Meldungen anzeigen |

Exit-Codes: `0` Erfolg, `1` Score nicht konvertierbar, `2` ungültige Argumente oder Dateifehler. Die CLI antwortet je nach Systemsprache auf Deutsch oder Englisch.

### In Logic Pro

Die MIDI-Datei am besten über *Ablage → Öffnen* öffnen: Logic legt dann ein neues Projekt an, das Tempo, Taktart und Marker ab Takt 1 aus der Datei übernimmt. Zieht man die Datei stattdessen in ein bestehendes Projekt, genau auf Takt 1 ablegen und die Tempo-Übernahme bestätigen; Tempo und Taktart werden relativ zur Ablageposition eingefügt, davor gilt weiter das Projekttempo. Anschließend `audio.flac` aus demselben YuE-Ausgabeordner auf eine neue Audiospur bei Takt 1 ziehen. Da die MIDI-Datei das Tempo aus dem Score mitbringt, laufen beide synchron.

## Inhalt der MIDI-Datei

Eine Standard-MIDI-Datei vom Typ 1:

| Spur | Inhalt |
|---|---|
| `Conductor` | Tempo, Taktart, Tonart und ein Marker pro Abschnitt (`verse`, `chorus`, …); Logic übernimmt sie in die globalen Spuren |
| `Vocal` | Die Gesangsmelodie |
| `Ins` | Die Instrumentalmelodie |
| `Chords` | Die Akkordsymbole als Blockakkorde (Grundton in Oktave 3, Slash-Bass darunter), zusätzlich das Symbol als Text-Event |
| `Bass` | Nur mit `--bass`: der Basston jedes Akkords im Register E2–D♯3 (MIDI 40–51, in Logics Benennung E1–D♯2), das jedes Bassinstrument spielen kann; mit `--bass-octave -1` geht es bis zur tiefsten E-Bass-Saite hinunter. Slash-Akkorde wie `C/E` spielen ihren Basston. Die Noten sind leicht gekürzt, Zählzeiten etwas lauter. `root-fifth` wechselt in Vierteln zwischen Basston und Quinte des Akkords |
| `Drums` | Nur mit `--drums`: Bassdrum auf jedem Schlag, Snare auf 2 und 4, geschlossene Hi-Hat in Achteln (Offbeats leiser) und ein Crash-Becken zu Beginn jedes Abschnitts. General-MIDI-Notennummern auf Kanal 10, die Logics Drumkits verstehen. Im 3/4-Takt spielt die Snare auf 2; 6/8 wird in punktierten Vierteln gezählt |

Die Arrangement-Optionen stehen auch in der Bibliothek zur Verfügung (`ConversionOptions.Arrangement`), und die erzeugten Spuren erscheinen in der JSON-Ausgabe mit `"kind": "Bass"` bzw. `"Drums"`.

## Das Eingabeformat

YuE2 schreibt eine bewusst kleine Teilmenge der ABC-Notation. Ein allgemeiner ABC-Parser würde sie falsch lesen: Vor allem gilt ein Vorzeichen für seinen Notenbuchstaben **in allen Oktaven** bis zum Taktstrich (nach `^F` ist auch `f` erhöht). Der Parser in diesem Projekt folgt den YuE2-Regeln und meldet alles, was davon abweicht, als Diagnose, statt abzubrechen – auch von Hand bearbeitete Scores lassen sich also konvertieren. Die vollständigen Regeln stehen in der [ABC-Referenz von YuE2](https://github.com/multimodal-art-projection/YuE/blob/main/skills/yue2-music/references/abc-editing.md).

## Projektaufbau

```
src/YueToLogic.Core/    Bibliothek: ABC-Parser, Score-Modell, MIDI-Erzeugung
src/YueToLogic.Cli/     Kommandozeilenwerkzeug (yue2logic)
tests/                  xUnit-Tests
samples/score.abc       Offizielles YuE2-Beispiel
```

`YueToLogic.Core` hat keine Abhängigkeiten zu Konsole oder Dateisystem, damit die Bibliothek später aus einem Webservice, einem Electron/Vue-Frontend oder einer macOS-App genutzt werden kann:

- Eingabe ist ein `string` oder `Stream`, Ausgabe ein `byte[]` oder ein vom Aufrufer gestellter `Stream`.
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
```

## Nächste Schritte

Ein vollständiges `.logicx`-Projekt erzeugen. Logics Projektformat ist ein undokumentiertes Binärpaket; der wahrscheinliche nächste Schritt ist daher ein Ausgabeordner mit MIDI-Datei und kopiertem Audio, den man in Logic in einem Rutsch importiert.
