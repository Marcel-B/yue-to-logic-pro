import type { BassPattern, ChordInversion, ChordPattern, ConversionOptions, DrumPattern, SwingUnit } from './types'

/** What `--humanize 100` means in the CLI, so both hosts scatter the notes by the same amount. */
const maxHumanizeTimingMs = 25
const maxHumanizeVelocity = 24

/** The parameter form as the UI edits it; converted to the API's ConversionOptions on submit. */
export interface FormState {
  includeChords: boolean
  octave: number
  /** null = same as `octave`. */
  vocalOctave: number | null
  insOctave: number | null
  bass: 'off' | BassPattern
  bassOctave: number
  drums: 'off' | DrumPattern
  crash: boolean
  /** 'as-written' plays one sustained block chord per symbol, as the score notates it. */
  chordPattern: 'as-written' | ChordPattern
  chordInversion: ChordInversion
  chordOctave: number
  guideTones: boolean
  guideOctave: number
  doubleVocal: boolean
  doubleOctave: number
  /** Swing in percent: 0 straight, 100 a full triplet feel. */
  swing: number
  swingUnit: SwingUnit
  straightDrums: boolean
  /** Humanization in percent of the maxima above. */
  humanize: number
  mono: boolean
  legato: boolean
  /** One region per song section in the Logic project; used by the Logic export only. */
  splitSections: boolean
  ppq: number
}

export const defaultFormState = (): FormState => ({
  includeChords: true,
  octave: 0,
  vocalOctave: null,
  insOctave: null,
  bass: 'off',
  bassOctave: 0,
  drums: 'off',
  crash: true,
  chordPattern: 'as-written',
  chordInversion: 'RootPosition',
  chordOctave: 0,
  guideTones: false,
  guideOctave: 0,
  doubleVocal: false,
  doubleOctave: -1,
  swing: 0,
  swingUnit: 'Eighths',
  straightDrums: false,
  humanize: 0,
  mono: false,
  legato: false,
  splitSections: false,
  ppq: 480,
})

export function toConversionOptions(form: FormState): ConversionOptions {
  const octaveShifts: Record<string, number> = {}
  if (form.vocalOctave !== null) {
    octaveShifts.Vocal = form.vocalOctave
  }
  if (form.insOctave !== null) {
    octaveShifts.Ins = form.insOctave
  }

  // A voicing or a register alone is reason enough for a chord track; it then plays the chords as written.
  const chordsAsWritten = form.chordPattern === 'as-written'
  const plainChords = chordsAsWritten && form.chordInversion === 'RootPosition' && form.chordOctave === 0

  return {
    ticksPerQuarterNote: form.ppq,
    includeChordTrack: form.includeChords,
    arrangement: {
      defaultOctaveShift: form.octave,
      octaveShifts,
      bass: form.bass === 'off' ? null : { pattern: form.bass, octaveShift: form.bassOctave },
      drums: form.drums === 'off' ? null : { pattern: form.drums, crashOnSections: form.crash },
      chords:
        !form.includeChords || plainChords
          ? null
          : {
              // Compared inline rather than through `chordsAsWritten`, so the type narrows to a ChordPattern.
              pattern: form.chordPattern === 'as-written' ? 'Block' : form.chordPattern,
              inversion: form.chordInversion,
              octaveShift: form.chordOctave,
            },
      guideTones: form.guideTones ? { octaveShift: form.guideOctave } : null,
      doubling: form.doubleVocal ? { voiceId: 'Vocal', semitones: 12 * form.doubleOctave } : null,
      groove:
        form.swing > 0 || form.humanize > 0
          ? {
              swing: form.swing / 100,
              swingUnit: form.swingUnit,
              includeDrums: !form.straightDrums,
              humanizeTimingMs: (form.humanize / 100) * maxHumanizeTimingMs,
              humanizeVelocity: Math.round((form.humanize / 100) * maxHumanizeVelocity),
            }
          : null,
      mono: form.mono ? { legato: form.legato } : null,
    },
  }
}

const storageKey = 'yue-to-logic.options'

/** The last used parameters, so repeated conversions start from the same settings. */
export function loadFormState(): FormState {
  try {
    const stored = localStorage.getItem(storageKey)
    if (stored) {
      const state = { ...defaultFormState(), ...(JSON.parse(stored) as Partial<FormState>) }
      // Settings saved before drum patterns existed stored the drum switch as a boolean.
      const drums = state.drums as unknown
      if (typeof drums === 'boolean') {
        state.drums = drums ? 'FourOnTheFloor' : 'off'
      }
      return state
    }
  } catch {
    // Unavailable or corrupt storage: start with the defaults.
  }
  return defaultFormState()
}

export function clearFormState(): void {
  try {
    localStorage.removeItem(storageKey)
  } catch {
    // Nothing stored or storage unavailable.
  }
}

export function saveFormState(form: FormState): void {
  try {
    localStorage.setItem(storageKey, JSON.stringify(form))
  } catch {
    // Remembering the parameters is a convenience only.
  }
}
