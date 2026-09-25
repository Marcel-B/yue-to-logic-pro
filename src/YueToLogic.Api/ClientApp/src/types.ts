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
    drums: { pattern: DrumPattern; crashOnSections: boolean; separateTracks: boolean; notes: DrumNotes | null } | null
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
  /** MIDI channel (1-16) per track name; a track without an entry gets the next free one. */
  midiChannels: Record<string, number>
  /** Program change (1-128) sent at the start of the track, per track name. */
  midiPrograms: Record<string, number>
}

/** What a track of a MIDI file becomes in the score.abc (MidiTrackRole in the core library). */
export type MidiTrackRole = 'Ignore' | 'Vocal' | 'Ins' | 'Chords'

/** A track of a MIDI file on the way back: a track, or one channel of a track that plays on several. */
export interface MidiTrackInfo {
  /** What `MidiToAbcOptions.trackRoles` refers to it by. */
  index: number
  name: string
  /** 1-16. */
  channel: number
  noteCount: number
  /** Whether it mostly strikes several notes at once, as a chord track does. */
  polyphonic: boolean
  role: MidiTrackRole
}

export interface MidiToAbcOptions {
  /** Role per track index; tracks without an entry keep the role their name suggests. */
  trackRoles: Record<number, MidiTrackRole>
  /** Bars left out at the start; null leaves out the silent ones (a count-in). */
  skipBars: number | null
}

export interface MidiToAbcResult {
  success: boolean
  /** The score.abc for YuE2. */
  abc: string | null
  score: ScoreDocument | null
  /** Every track with notes, also when conversion failed, so the roles can be chosen by hand. */
  tracks: MidiTrackInfo[]
  diagnostics: Diagnostic[]
}

/** A stem separation job as the backend reports it. */
export interface StemJob {
  id: string
  /** queued, processing, completed or failed. */
  status: StemJobStatus | string
  attempts: number
  lastError: string | null
  /** ISO 8601; when the recording was handed over. Only the list of jobs carries the timestamps. */
  createdUtc: string | null
  /** ISO 8601; when the service last changed the job. */
  updatedUtc: string | null
  /** The separation model the job runs with, as the service names it back. */
  model: string | null
}

export type StemJobStatus = 'queued' | 'processing' | 'completed' | 'failed'

/**
 * A separation model the stem service offers. Which one is chosen decides how long the job runs and which
 * stems come back, so the choice shows more than the name.
 */
export interface SeparationModel {
  /** What a job is started with. */
  id: string
  name: string
  family: string | null
  /** vocals, instrumental, karaoke, 4stem, 6stem or drums. */
  task: string | null
  /** The files of the result ZIP, each without its .wav ending. */
  stems: string[]
  /** fast, moderate, slow or verySlow. */
  speed: string | null
  /** Audio length divided by computing time: 0.3 means a four-minute song takes about thirteen minutes. */
  realtimeFactor: number | null
  /** False when the estimate comes from a comparable model rather than a measurement. */
  measured: boolean
  notes: string | null
  /** The model a job without a choice runs with. */
  isDefault: boolean
}

/** What a recording is made of, as the voice service reads it. */
export interface VoiceAudioProperties {
  /** The codec, e.g. `pcm_s16le` or `mp3`. */
  codec: string
  durationSeconds: number
  sampleRate: number
  channels: number
}

/** A stored reference voice: the collection a conversion chooses its timbre from. */
export interface ReferenceVoice {
  /** What a job is started with. */
  id: string
  label: string
  /** ISO 8601. */
  createdUtc: string | null
  /** What the service keeps — always mono PCM at 44.1 kHz. */
  stored: VoiceAudioProperties | null
  /** The file as it was uploaded; it says whether the recording was good enough. */
  original: VoiceAudioProperties | null
}

/** A voice conversion job as the backend reports it. */
export interface VoiceJob {
  id: string
  /** QUEUED, RUNNING, COMPLETED, FAILED or CANCELLED. */
  status: VoiceJobStatus | string
  voiceId: string | null
  /** The reference voice's name when the job was accepted; it may be gone by now. */
  voiceLabel: string | null
  /** ISO 8601. */
  createdUtc: string | null
  startedUtc: string | null
  finishedUtc: string | null
  /** The service's own code for the failure, if the job failed. */
  errorCode: string | null
  errorMessage: string | null
  resultSizeBytes: number | null
  /**
   * Whether the converted recording can still be fetched. The service keeps a record of every job it ever
   * had, so one whose files are gone stays in the list; this is what tells the two apart.
   */
  hasResult: boolean
}

export type VoiceJobStatus = 'QUEUED' | 'RUNNING' | 'COMPLETED' | 'FAILED' | 'CANCELLED'

/**
 * One page of the voice service's jobs. It keeps a record of every job it ever had, so the list only grows
 * and is read page by page; `total` counts every job the filter matches, not the ones on this page.
 */
export interface VoiceJobPage {
  jobs: VoiceJob[]
  total: number
  limit: number
  offset: number
}

/**
 * The diagnostic codes the interface acts on rather than only shows. They are part of the contract - see
 * `DiagnosticCodes` in the core library - and each one means that the export left something at its service
 * instead of taking it into the project.
 */
export const STEMS_NOT_TAKEN = 'YTL054'
export const VOICE_NOT_TAKEN = 'YTL056'

/** The tracks a conversion can produce, in the order the MIDI file lists them. */
export const TRACK_NAMES = ['Vocal', 'Ins', 'Vocal 8vb', 'Chords', 'Bass', 'Drums', 'Guide', 'Kick', 'Snare', 'HiHat', 'Crash'] as const

/** The note each drum of the generated kit is played on; General MIDI unless a drum machine says otherwise. */
export interface DrumNotes {
  kick: number
  snare: number
  closedHiHat: number
  openHiHat: number
  crash: number
  clap: number
}

/** The General MIDI drum map, what the generator plays without a drum machine. */
export const GENERAL_MIDI_DRUMS: DrumNotes = { kick: 36, snare: 38, closedHiHat: 42, openHiHat: 46, crash: 49, clap: 39 }

/** The drums of the kit in the order the interface lists them. */
export const DRUMS = ['kick', 'snare', 'closedHiHat', 'openHiHat', 'crash', 'clap'] as const satisfies readonly (keyof DrumNotes)[]

/**
 * A synthesizer plays a track on its port and channel; a drum machine plays several drums on one channel,
 * each on a note of its own, which the drum tracks that play it are generated on.
 */
export type InstrumentKind = 'Synth' | 'DrumMachine'

/** A hardware instrument as the server keeps it: a name for a MIDI port (as Web MIDI names it) and a channel, 1-16. */
export interface Instrument {
  id: number
  name: string
  port: string
  channel: number
  kind: InstrumentKind
  /** Only a drum machine has them. */
  drums: DrumNotes | null
}

export interface InstrumentInput {
  name: string
  port: string
  channel: number
  kind: InstrumentKind
  drums: DrumNotes | null
}

/** Track name → id of the instrument that plays it. */
export type Assignments = Record<string, number>

/** What the Logic export is told about a track's instrument. */
export interface LogicInstrument {
  name: string
  port: string
  channel: number
}
