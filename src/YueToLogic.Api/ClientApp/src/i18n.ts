import { ref } from 'vue'

export type Locale = 'de' | 'en'

const messages = {
  de: {
    subtitle: 'YuE2-score.abc in eine MIDI-Datei für Logic Pro umwandeln',
    reset: 'Zurücksetzen',
    resetTitle: 'Dateien, Parameter und Ergebnis zurücksetzen',
    scoreTitle: 'Score',
    dropHint: 'score.abc hierher ziehen',
    dropOr: 'oder',
    chooseFile: 'Datei auswählen',
    otherFile: 'Andere Datei',
    removeFile: 'Entfernen',
    audioTitle: 'Audio (optional)',
    audioInfo: 'Für ein Logic-Projekt: die audio.flac aus demselben YuE-Ordner wie die score.abc.',
    audioDropHint: 'audio.flac hierher ziehen',
    notFlac: 'Die Datei endet nicht auf .flac – YuE schreibt das Audio als audio.flac.',
    downloadLogic: 'Logic-Projekt herunterladen',
    buildingLogic: 'Logic-Projekt wird erstellt …',
    logicNeedsAudio: 'Für ein Logic-Projekt oben die audio.flac hinzufügen.',
    logicHint: 'Experimentell: Das Projekt entsteht aus einer Vorlage aus Logic Pro 12.3 mit Audiospur und fünf Instrumentenspuren.',
    logicWarnings: 'Hinweise zum Logic-Projekt',
    logicFailed: 'Das Logic-Projekt konnte nicht erstellt werden',
    dropWhileDragging: 'Loslassen zum Übernehmen',
    notAbc: 'Die Datei endet nicht auf .abc – YuE2 schreibt den Score als score.abc.',
    optionsTitle: 'Parameter',
    tracks: 'Spuren',
    includeChords: 'Akkordspur (Akkordsymbole als Blockakkorde)',
    octaves: 'Oktavlage',
    octaveBoth: 'Beide Stimmen',
    octaveVocal: 'Gesang',
    octaveIns: 'Instrument',
    sameAsBoth: 'wie „Beide“',
    bass: 'Bass',
    bassOff: 'Kein Bass',
    bassEighths: 'Grundton in Achteln',
    bassQuarters: 'Grundton in Vierteln',
    bassRootFifth: 'Grundton und Quinte',
    bassOctave: 'Oktave',
    drums: 'Schlagzeug',
    drumsOff: 'Kein Schlagzeug',
    drumsOn: 'Four on the Floor (Kick auf jedem Schlag, Snare auf 2 und 4, Hi-Hat)',
    drumsBackbeat: 'Backbeat (Kick auf 1 und 3, Snare auf 2 und 4, offene Hi-Hat auf 4+)',
    crash: 'Crash-Becken zu Beginn jedes Abschnitts',
    advanced: 'Erweitert',
    ppq: 'Auflösung (Ticks pro Viertel)',
    outputName: 'Dateiname',
    convert: 'Konvertieren',
    converting: 'Wird konvertiert …',
    stale: 'Parameter geändert – erneut konvertieren, um die Downloads zu aktualisieren.',
    resultTitle: 'Ergebnis',
    failedTitle: 'Konvertierung fehlgeschlagen',
    tempo: 'Tempo',
    meter: 'Taktart',
    key: 'Tonart',
    length: 'Länge',
    lengthValue: '{bars} Takte · {duration}',
    sections: 'Abschnitte · Starttakt',
    noSections: 'keine',
    bar: 'Takt {bar}',
    trackList: 'Spuren',
    notes: '{count} Noten',
    chords: '{count} Akkorde',
    chordTrack: 'Akkorde',
    downloadMidi: 'MIDI herunterladen',
    downloadJson: 'JSON herunterladen',
    diagnostics: 'Meldungen',
    noDiagnostics: 'Keine Meldungen – der Score entspricht dem YuE2-Format.',
    showInfos: '{count} Info-Meldung(en) anzeigen',
    severity_Error: 'Fehler',
    severity_Warning: 'Warnung',
    severity_Info: 'Info',
    location: 'Zeile {line}',
    locationColumn: 'Zeile {line}, Spalte {column}',
    networkError: 'Das Backend ist nicht erreichbar. Läuft „dotnet run --project src/YueToLogic.Api“?',
    requestError: 'Die Anfrage wurde abgelehnt: {message}',
  },
  en: {
    subtitle: 'Convert a YuE2 score.abc into a MIDI file for Logic Pro',
    reset: 'Reset',
    resetTitle: 'Clear files, parameters and result',
    scoreTitle: 'Score',
    dropHint: 'Drop score.abc here',
    dropOr: 'or',
    chooseFile: 'Choose file',
    otherFile: 'Other file',
    removeFile: 'Remove',
    audioTitle: 'Audio (optional)',
    audioInfo: 'For a Logic project: the audio.flac from the same YuE folder as the score.abc.',
    audioDropHint: 'Drop audio.flac here',
    notFlac: 'The file does not end in .flac – YuE writes the audio as audio.flac.',
    downloadLogic: 'Download Logic project',
    buildingLogic: 'Creating Logic project …',
    logicNeedsAudio: 'Add the audio.flac above to get a Logic project.',
    logicHint: 'Experimental: the project is built from a Logic Pro 12.3 template with an audio track and five instrument tracks.',
    logicWarnings: 'Notes on the Logic project',
    logicFailed: 'The Logic project could not be created',
    dropWhileDragging: 'Release to use this file',
    notAbc: 'The file does not end in .abc – YuE2 writes the score as score.abc.',
    optionsTitle: 'Parameters',
    tracks: 'Tracks',
    includeChords: 'Chord track (chord symbols as block chords)',
    octaves: 'Octave',
    octaveBoth: 'Both voices',
    octaveVocal: 'Vocal',
    octaveIns: 'Instrument',
    sameAsBoth: 'same as “Both”',
    bass: 'Bass',
    bassOff: 'No bass',
    bassEighths: 'Root in eighth notes',
    bassQuarters: 'Root in quarter notes',
    bassRootFifth: 'Root and fifth',
    bassOctave: 'Octave',
    drums: 'Drums',
    drumsOff: 'No drums',
    drumsOn: 'Four on the floor (kick on every beat, snare on 2 and 4, hi-hat)',
    drumsBackbeat: 'Backbeat (kick on 1 and 3, snare on 2 and 4, open hi-hat on 4+)',
    crash: 'Crash cymbal at the start of every section',
    advanced: 'Advanced',
    ppq: 'Resolution (ticks per quarter note)',
    outputName: 'File name',
    convert: 'Convert',
    converting: 'Converting …',
    stale: 'Parameters changed – convert again to update the downloads.',
    resultTitle: 'Result',
    failedTitle: 'Conversion failed',
    tempo: 'Tempo',
    meter: 'Meter',
    key: 'Key',
    length: 'Length',
    lengthValue: '{bars} bars · {duration}',
    sections: 'Sections · starting bar',
    noSections: 'none',
    bar: 'bar {bar}',
    trackList: 'Tracks',
    notes: '{count} notes',
    chords: '{count} chords',
    chordTrack: 'Chords',
    downloadMidi: 'Download MIDI',
    downloadJson: 'Download JSON',
    diagnostics: 'Messages',
    noDiagnostics: 'No messages – the score matches the YuE2 format.',
    showInfos: 'Show {count} informational message(s)',
    severity_Error: 'Error',
    severity_Warning: 'Warning',
    severity_Info: 'Info',
    location: 'line {line}',
    locationColumn: 'line {line}, column {column}',
    networkError: 'Cannot reach the backend. Is “dotnet run --project src/YueToLogic.Api” running?',
    requestError: 'The request was rejected: {message}',
  },
} as const

export type MessageKey = keyof (typeof messages)['de']

const storageKey = 'yue-to-logic.locale'

function initialLocale(): Locale {
  try {
    const stored = localStorage.getItem(storageKey)
    if (stored === 'de' || stored === 'en') {
      return stored
    }
  } catch {
    // Storage may be unavailable (private mode); fall back to the browser language.
  }
  return navigator.language.toLowerCase().startsWith('de') ? 'de' : 'en'
}

export const locale = ref<Locale>(initialLocale())

export function setLocale(value: Locale): void {
  locale.value = value
  document.documentElement.lang = value
  try {
    localStorage.setItem(storageKey, value)
  } catch {
    // Remembering the choice is a convenience only.
  }
}

export function t(key: MessageKey, params: Record<string, string | number> = {}): string {
  return messages[locale.value][key].replace(/\{(\w+)\}/g, (_, name: string) => String(params[name] ?? `{${name}}`))
}

export function formatNumber(value: number, maximumFractionDigits = 1): string {
  return value.toLocaleString(locale.value, { maximumFractionDigits })
}

/** 309.4 → "5:09" */
export function formatDuration(seconds: number): string {
  const total = Math.round(seconds)
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`
}
