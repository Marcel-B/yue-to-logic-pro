<script setup lang="ts">
import { computed, ref, useTemplateRef } from 'vue'
import { ApiError, createInstrument, deleteInstrument, listInstruments, updateInstrument } from '../api'
import { t } from '../i18n'
import { listMidiPorts, midiAlreadyAllowed, midiSupported, type MidiPort } from '../player'
import type { Instrument } from '../types'

/**
 * The instrument library: a name for each MIDI port and channel the user's hardware listens on. Kept on the
 * server, so the list is the same from every browser; this dialog only edits it.
 */
const props = defineProps<{ instruments: Instrument[] }>()

/** The list after any change, fetched anew so that ids and order are the server's. */
const emit = defineEmits<{ changed: [instruments: Instrument[]] }>()

/** The port select's value for a port typed by hand, e.g. for an interface that is not plugged in right now. */
const OTHER_PORT = '\u0000other'

const dialog = useTemplateRef<HTMLDialogElement>('dialog')
const ports = ref<MidiPort[]>([])
const canAskForMidi = ref(false)
/** The id being edited, or null while adding. */
const editing = ref<number | null>(null)
const name = ref('')
const port = ref<string>(OTHER_PORT)
const otherPort = ref('')
const channel = ref(1)
const busy = ref(false)
const error = ref<string | null>(null)

const channels = Array.from({ length: 16 }, (_, index) => index + 1)

/** Port names the browser offers, each once; two identical interfaces cannot be told apart by name anyway. */
const portNames = computed(() => [...new Set(ports.value.map((entry) => entry.name.trim()))])
const chosenPort = computed(() => (port.value === OTHER_PORT ? otherPort.value : port.value).trim())
const complete = computed(() => name.value.trim().length > 0 && chosenPort.value.length > 0)

async function open(): Promise<void> {
  startAdding()
  error.value = null
  dialog.value?.showModal()
  if (midiSupported() && (await midiAlreadyAllowed())) {
    await loadPorts()
  } else {
    canAskForMidi.value = midiSupported()
  }
}

function close(): void {
  dialog.value?.close()
}

async function loadPorts(): Promise<void> {
  const found = await listMidiPorts()
  ports.value = found.ports
  canAskForMidi.value = false
  // A form opened before the ports were known can now offer the typed port from the list.
  if (port.value === OTHER_PORT && portNames.value.includes(otherPort.value.trim())) {
    port.value = otherPort.value.trim()
  } else if (port.value === OTHER_PORT && otherPort.value === '' && portNames.value.length > 0) {
    port.value = portNames.value[0]!
  }
}

function startAdding(): void {
  editing.value = null
  name.value = ''
  port.value = portNames.value[0] ?? OTHER_PORT
  otherPort.value = ''
  channel.value = 1
  error.value = null
}

function edit(instrument: Instrument): void {
  editing.value = instrument.id
  name.value = instrument.name
  channel.value = instrument.channel
  error.value = null
  if (portNames.value.includes(instrument.port)) {
    port.value = instrument.port
    otherPort.value = ''
  } else {
    // The stored port is not connected right now; the name still shows, and can be changed.
    port.value = OTHER_PORT
    otherPort.value = instrument.port
  }
}

async function submit(): Promise<void> {
  if (!complete.value || busy.value) {
    return
  }

  busy.value = true
  error.value = null
  const input = { name: name.value.trim(), port: chosenPort.value, channel: channel.value }
  try {
    if (editing.value === null) {
      await createInstrument(input)
    } else {
      await updateInstrument(editing.value, input)
    }
    await refresh()
    startAdding()
  } catch (caught) {
    fail(caught)
  } finally {
    busy.value = false
  }
}

async function remove(instrument: Instrument): Promise<void> {
  if (busy.value || !window.confirm(t('instrumentDeleteConfirm', { name: instrument.name }))) {
    return
  }

  busy.value = true
  error.value = null
  try {
    await deleteInstrument(instrument.id)
    await refresh()
    if (editing.value === instrument.id) {
      startAdding()
    }
  } catch (caught) {
    fail(caught)
  } finally {
    busy.value = false
  }
}

async function refresh(): Promise<void> {
  emit('changed', await listInstruments())
}

function fail(caught: unknown): void {
  error.value =
    caught instanceof ApiError && caught.status === 409
      ? t('instrumentNameTaken')
      : caught instanceof ApiError && caught.status === 0
        ? t('networkError')
        : `${caught instanceof Error ? caught.message : caught}`
}

defineExpose({ open })
</script>

<template>
  <dialog ref="dialog" class="instrument-dialog" @cancel.prevent="close">
    <h3>{{ t('instrumentsTitle') }}</h3>
    <p class="intro">{{ t('instrumentsIntro') }}</p>

    <table v-if="props.instruments.length > 0">
      <thead>
        <tr>
          <th>{{ t('instrumentName') }}</th>
          <th>{{ t('instrumentPort') }}</th>
          <th>{{ t('instrumentChannel') }}</th>
          <th><span class="sr-only">{{ t('instrumentEdit') }}</span></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="instrument in props.instruments" :key="instrument.id" :class="{ editing: instrument.id === editing }">
          <td>{{ instrument.name }}</td>
          <td>{{ instrument.port }}</td>
          <td>{{ instrument.channel }}</td>
          <td class="actions">
            <button type="button" class="link" :disabled="busy" @click="edit(instrument)">{{ t('instrumentEdit') }}</button>
            <button type="button" class="link" :disabled="busy" @click="remove(instrument)">{{ t('instrumentDelete') }}</button>
          </td>
        </tr>
      </tbody>
    </table>
    <p v-else class="muted empty">{{ t('instrumentsEmpty') }}</p>

    <form class="editor" @submit.prevent="submit">
      <h4>{{ editing === null ? t('instrumentAdd') : t('instrumentEditing', { name }) }}</h4>
      <label>
        {{ t('instrumentName') }}
        <input v-model.trim="name" type="text" required maxlength="64" spellcheck="false" placeholder="Mother32" />
      </label>
      <label>
        {{ t('instrumentPort') }}
        <select v-model="port">
          <option v-for="entry in portNames" :key="entry" :value="entry">{{ entry }}</option>
          <option :value="OTHER_PORT">{{ t('instrumentPortOther') }}</option>
        </select>
      </label>
      <label v-if="port === OTHER_PORT">
        <span class="sr-only">{{ t('instrumentPort') }}</span>
        <input v-model.trim="otherPort" type="text" required maxlength="128" spellcheck="false" placeholder="MIDI4x4 Midi Out 1" />
      </label>
      <label>
        {{ t('instrumentChannel') }}
        <select v-model.number="channel">
          <option v-for="entry in channels" :key="entry" :value="entry">{{ entry }}</option>
        </select>
      </label>
      <p class="muted hint">{{ t('instrumentPortHint') }}</p>
      <div class="choices">
        <button v-if="canAskForMidi" type="button" class="button secondary" @click="loadPorts">{{ t('previewFindMidi') }}</button>
        <button type="submit" class="button primary" :disabled="!complete || busy">
          {{ editing === null ? t('instrumentAdd') : t('instrumentSave') }}
        </button>
        <button v-if="editing !== null" type="button" class="button secondary" @click="startAdding">{{ t('instrumentCancel') }}</button>
        <button type="button" class="button secondary" @click="close">{{ t('instrumentClose') }}</button>
      </div>
      <p v-if="error" class="hint danger" role="alert">{{ error }}</p>
    </form>
  </dialog>
</template>

<style scoped>
.instrument-dialog {
  width: min(36rem, calc(100vw - 2rem));
  padding: 1.25rem;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: var(--surface);
  color: var(--text);
}

.instrument-dialog::backdrop {
  background: rgb(0 0 0 / 50%);
}

h3 {
  margin: 0 0 0.35rem;
  font-size: 1.1rem;
}

h4 {
  margin: 0 0 0.5rem;
  font-size: 0.95rem;
}

.intro {
  margin: 0 0 1rem;
  color: var(--text-muted);
  font-size: 0.9rem;
}

table {
  width: 100%;
  margin-bottom: 1rem;
  border-collapse: collapse;
}

th {
  padding: 0.35rem 0.5rem 0.35rem 0;
  color: var(--text-muted);
  font-size: 0.8125rem;
  font-weight: 500;
  text-align: left;
}

td {
  padding: 0.3rem 0.5rem 0.3rem 0;
  border-top: 1px solid var(--border);
  vertical-align: middle;
}

tr.editing td {
  background: var(--accent-soft);
}

.actions {
  display: flex;
  gap: 0.75rem;
  white-space: nowrap;
}

.empty {
  margin: 0 0 1rem;
  font-size: 0.9rem;
}

.editor {
  display: grid;
  gap: 0.6rem;
  padding-top: 1rem;
  border-top: 1px solid var(--border);
}

.editor label {
  display: grid;
  gap: 0.25rem;
  font-size: 0.9rem;
}

.editor .hint {
  margin: 0;
}

.choices {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-top: 0.25rem;
}
</style>
