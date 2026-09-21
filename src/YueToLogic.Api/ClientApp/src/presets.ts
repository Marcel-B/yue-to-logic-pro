import { defaultFormState, type FormState } from './options'

/**
 * Named parameter sets, kept in the browser beside the last used settings. A preset holds the whole form,
 * so a song can be converted the same way again without clicking through every option.
 */
export interface Preset {
  name: string
  form: FormState
}

const storageKey = 'yue-to-logic.presets'

export function loadPresets(): Preset[] {
  try {
    const stored = localStorage.getItem(storageKey)
    const presets = stored ? (JSON.parse(stored) as Preset[]) : []
    // Settings saved before an option existed fall back to its default, as the last used ones do.
    return presets
      .filter((preset) => typeof preset?.name === 'string' && preset.form)
      .map((preset) => ({ name: preset.name, form: { ...defaultFormState(), ...preset.form } }))
  } catch {
    return []
  }
}

/** Adds the preset, or replaces the one of the same name; returns the list as it now stands. */
export function savePreset(name: string, form: FormState): Preset[] {
  const presets = loadPresets().filter((preset) => preset.name !== name);
  presets.push({ name, form: { ...form } })
  presets.sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true }))
  return write(presets)
}

export function deletePreset(name: string): Preset[] {
  return write(loadPresets().filter((preset) => preset.name !== name))
}

function write(presets: Preset[]): Preset[] {
  try {
    localStorage.setItem(storageKey, JSON.stringify(presets))
  } catch {
    // Keeping presets is a convenience; without storage they last until the page is reloaded.
  }
  return presets
}
