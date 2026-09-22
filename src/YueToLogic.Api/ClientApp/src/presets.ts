import { listPresets, putPreset, removePreset, type StoredPreset } from './api'
import { defaultFormState, type FormState } from './options'

/**
 * Named parameter sets, so a song can be converted the same way again without clicking through every
 * option. They are kept on the server, like the instruments, so every browser the interface is opened from
 * offers the same ones; the server stores the form as it is and hands it back unread.
 */
export interface Preset {
  name: string
  form: FormState
}

/** Where presets lived before the server kept them; read once to move them over, then left empty. */
const legacyStorageKey = 'yue-to-logic.presets'

/**
 * The presets from the server. The first time a browser that still holds presets from before sees an
 * empty server, it hands them over, so that nothing is lost by the move; afterwards they are gone from it.
 */
export async function loadPresets(): Promise<Preset[]> {
  let presets = (await listPresets()).map(fromStored)
  if (presets.length === 0) {
    const legacy = takeLegacyPresets()
    if (legacy.length > 0) {
      for (const preset of legacy) {
        await putPreset(preset.name, preset.form)
      }
      presets = (await listPresets()).map(fromStored)
    }
  }
  return presets
}

/** Adds the preset, or replaces the one of the same name; resolves with the list as it now stands. */
export async function savePreset(name: string, form: FormState): Promise<Preset[]> {
  await putPreset(name, { ...form })
  return (await listPresets()).map(fromStored)
}

export async function deletePreset(name: string): Promise<Preset[]> {
  await removePreset(name)
  return (await listPresets()).map(fromStored)
}

/** Settings saved before an option existed fall back to its default, as the last used ones do. */
function fromStored(stored: StoredPreset): Preset {
  const form = stored.form && typeof stored.form === 'object' ? (stored.form as Partial<FormState>) : {}
  return { name: stored.name, form: { ...defaultFormState(), ...form } }
}

/** The presets an earlier version kept in the browser, removed from it on the way out. */
function takeLegacyPresets(): Preset[] {
  try {
    const stored = localStorage.getItem(legacyStorageKey)
    if (!stored) {
      return []
    }
    localStorage.removeItem(legacyStorageKey)
    return (JSON.parse(stored) as Preset[]).filter((preset) => typeof preset?.name === 'string' && preset.form)
  } catch {
    return []
  }
}
