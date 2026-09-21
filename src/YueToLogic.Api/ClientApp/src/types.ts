// Mirrors the JSON contract of YueToLogic.Core (camelCase, enums as strings).

export type Severity = 'Info' | 'Warning' | 'Error'

export interface Diagnostic {
  severity: Severity
  code: string
  message: string
  line: number | null
  column: number | null
}

export interface NoteEvent {
  startTicks: number
  durationTicks: number
  noteNumber: number
  velocity: number | null
}

export type TrackKind = 'Melody' | 'Chords' | 'Bass' | 'Drums' | 'GuideTones' | 'Doubling'

export interface VoiceTrack {
  id: string
  displayName: string
  notes: NoteEvent[]
  kind: TrackKind
}

export interface TimeSignatureChange {
  startTicks: number
  numerator: number
  denominator: number
}

export interface KeySignatureChange {
  startTicks: number
  key: string
  sharps: number
  isMinor: boolean
}

export interface SectionMarker {
  startTicks: number
  name: string
}

export interface ChordEvent {
  startTicks: number
  durationTicks: number
  text: string
  symbol: { rootPitchClass: number; quality: string; bassPitchClass: number | null } | null
}

export interface ScoreDocument {
  ticksPerQuarterNote: number
  title: string | null
  tempoBpm: number
  timeSignatures: TimeSignatureChange[]
  keySignatures: KeySignatureChange[]
  sections: SectionMarker[]
  voices: VoiceTrack[]
  chords: ChordEvent[]
  lengthTicks: number
  durationSeconds: number
}

export interface ConversionResult {
  success: boolean
  score: ScoreDocument | null
  /** Standard MIDI File, base64-encoded. */
  midi: string | null
  diagnostics: Diagnostic[]
}

export type BassPattern =
  | 'Eighths'
  | 'Quarters'
  | 'RootFifth'
  | 'Octaves'
  | 'Offbeat'
  | 'Sustained'
  | 'Walking'

export type DrumPattern = 'FourOnTheFloor' | 'Backbeat' | 'HalfTime' | 'Disco' | 'SixteenthHats' | 'Shuffle'

export type ChordPattern = 'Block' | 'Eighths' | 'Sixteenths' | 'Offbeat' | 'ArpeggioUp' | 'ArpeggioUpDown'

export type ChordInversion = 'RootPosition' | 'Closest' | 'First' | 'Second'

export type SwingUnit = 'Eighths' | 'Sixteenths'

export interface ConversionOptions {
  ticksPerQuarterNote: number
  includeChordTrack: boolean
  arrangement: {
    defaultOctaveShift: number
    octaveShifts: Record<string, number>
    bass: { pattern: BassPattern; octaveShift: number } | null
    drums: { pattern: DrumPattern; crashOnSections: boolean } | null
    chords: { pattern: ChordPattern; inversion: ChordInversion; octaveShift: number } | null
    guideTones: { octaveShift: number } | null
    doubling: { voiceId: string; semitones: number } | null
    groove: {
      swing: number
      swingUnit: SwingUnit
      includeDrums: boolean
      humanizeTimingMs: number
      humanizeVelocity: number
    } | null
    mono: { legato: boolean } | null
    countIn: { bars: number; click: boolean } | null
  }
  /** Set by the client once an audio file is chosen; the length is read from its FLAC header. */
  fitTempo: { audioSeconds: number } | null
  /** MIDI channel (1-16) per track name; a track without an entry gets the next free one. */
  midiChannels: Record<string, number>
  /** Program change (1-128) sent at the start of the track, per track name. */
  midiPrograms: Record<string, number>
}

/** The tracks a conversion can produce, in the order the MIDI file lists them. */
export const TRACK_NAMES = ['Vocal', 'Ins', 'Vocal 8vb', 'Chords', 'Bass', 'Drums', 'Guide'] as const
