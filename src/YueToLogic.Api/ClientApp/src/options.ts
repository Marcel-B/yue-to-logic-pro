import type { BassPattern, ChordPattern, ConversionOptions, DrumPattern } from './types'

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

  return {
    ticksPerQuarterNote: form.ppq,
    includeChordTrack: form.includeChords,
    arrangement: {
      defaultOctaveShift: form.octave,
      octaveShifts,
      bass: form.bass === 'off' ? null : { pattern: form.bass, octaveShift: form.bassOctave },
      drums: form.drums === 'off' ? null : { pattern: form.drums, crashOnSections: form.crash },
      chords:
        !form.includeChords || form.chordPattern === 'as-written' ? null : { pattern: form.chordPattern },
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
