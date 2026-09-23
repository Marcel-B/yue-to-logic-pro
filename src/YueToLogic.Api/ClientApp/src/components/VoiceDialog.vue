<script setup lang="ts">
import { ref, useTemplateRef } from 'vue'
import { addVoice, ApiError, deleteVoice, downloadVoiceAudio, listVoices } from '../api'
import { formatDateTime, formatDuration, formatNumber, t } from '../i18n'
import { download } from '../score'
import type { ReferenceVoice, VoiceAudioProperties } from '../types'

/**
 * The collection of model voices: one recording of a voice each, kept by the voice service under a name.
 * A conversion points at one of them, so this dialog is to voices what the instrument dialog is to the
 * hardware - the list everything else chooses from, and the only place it is edited.
 */
defineProps<{ voices: ReferenceVoice[] }>()

/** The collection after any change, fetched anew so that ids and order are the service's. */
const emit = defineEmits<{ changed: [voices: ReferenceVoice[]] }>()

const dialog = useTemplateRef<HTMLDialogElement>('dialog')
const fileInput = useTemplateRef<HTMLInputElement>('fileInput')
const label = ref('')
const file = ref<File | null>(null)
const busy = ref(false)
/** The voice being removed right now; its button is disabled meanwhile. */
const removing = ref<string | null>(null)
/** The voice being downloaded right now. */
const downloading = ref<string | null>(null)
const error = ref<string | null>(null)

function open(): void {
  clear()
  error.value = null
  dialog.value?.showModal()
}

function close(): void {
  dialog.value?.close()
}

function clear(): void {
  label.value = ''
  file.value = null
  if (fileInput.value) {
    fileInput.value.value = ''
  }
}

function chosen(event: Event): void {
  file.value = (event.target as HTMLInputElement).files?.[0] ?? null
}

async function add(): Promise<void> {
  if (!label.value.trim() || !file.value || busy.value) {
    return
  }

  busy.value = true
  error.value = null
  try {
    await addVoice(label.value.trim(), file.value)
    emit('changed', await listVoices())
    clear()
  } catch (caught) {
    fail(caught)
  } finally {
    busy.value = false
  }
}

/**
 * The recording as the service keeps it, which is what the model hears - so it is the one to listen to when a
 * conversion sounds wrong, not the file that was uploaded. Named after the voice, so it is found again.
 */
async function save(voice: ReferenceVoice): Promise<void> {
  if (downloading.value) {
    return
  }

  downloading.value = voice.id
  error.value = null
  try {
    download(await downloadVoiceAudio(voice.id), `${voice.label.replace(/[\\/:*?"<>|]/g, '_')}.wav`)
  } catch (caught) {
    if (caught instanceof ApiError && caught.status === 501) {
      error.value = t('voicesDownloadUnsupported')
    } else {
      fail(caught)
    }
  } finally {
    downloading.value = null
  }
}

/** Removes a voice; the service refuses while a job still waits for it, which it says with a 409. */
async function remove(voice: ReferenceVoice): Promise<void> {
  if (removing.value || !window.confirm(t('voicesDeleteConfirm', { label: voice.label }))) {
    return
  }

  removing.value = voice.id
  error.value = null
  try {
    await deleteVoice(voice.id)
    emit('changed', await listVoices())
  } catch (caught) {
    fail(caught)
  } finally {
    removing.value = null
  }
}

/** "0:18 · mp3 · 44,1 kHz · stereo", which says whether a recording is worth converting with. */
function summary(properties: VoiceAudioProperties | null): string {
  if (!properties) {
    return '–'
  }

  const channels = properties.channels === 1 ? 'mono' : properties.channels === 2 ? 'stereo' : `${properties.channels} ch`
  return [
    formatDuration(properties.durationSeconds),
    properties.codec,
    `${formatNumber(properties.sampleRate / 1000)} kHz`,
    channels,
  ]
    .filter((part) => part)
    .join(' · ')
}

function when(iso: string | null): string {
  return iso ? formatDateTime(iso) : '–'
}

function fail(caught: unknown): void {
  error.value =
    caught instanceof ApiError && caught.status === 0
      ? t('networkError')
      : caught instanceof ApiError && (caught.status === 429 || caught.status === 503)
        // A rate limit or a full queue: the same request is worth repeating later, unlike a refused recording.
        ? t('voiceRetryLater', { message: caught.message })
        : t('voicesError', { message: caught instanceof Error ? caught.message : String(caught) })
}

defineExpose({ open })
</script>

<template>
  <dialog ref="dialog" class="voice-collection" @cancel.prevent="close">
    <h3>{{ t('voicesTitle') }}</h3>
    <p class="intro">{{ t('voicesIntro') }}</p>

    <p v-if="error" class="hint danger" role="alert">{{ error }}</p>

    <table v-if="voices.length > 0">
      <thead>
        <tr>
          <th>{{ t('voicesVoice') }}</th>
          <th>{{ t('voicesAudio') }}</th>
          <th>{{ t('voicesCreated') }}</th>
          <th><span class="sr-only">{{ t('voicesActions') }}</span></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="entry in voices" :key="entry.id">
          <td>{{ entry.label }}</td>
          <td class="audio">
            {{ summary(entry.stored) }}
            <span v-if="entry.original" class="original">{{ t('voicesOriginal', { summary: summary(entry.original) }) }}</span>
          </td>
          <td class="time">{{ when(entry.createdUtc) }}</td>
          <td class="actions">
            <button type="button" class="link" :title="t('voicesDownloadTitle')" :disabled="downloading !== null" @click="save(entry)">
              {{ downloading === entry.id ? t('voicesDownloading') : t('voicesDownload') }}
            </button>
            <button type="button" class="link" :disabled="removing !== null || busy" @click="remove(entry)">
              {{ t('voicesDelete') }}
            </button>
          </td>
        </tr>
      </tbody>
    </table>
    <p v-else class="muted empty">{{ t('voicesEmpty') }}</p>

    <form class="add" @submit.prevent="add">
      <label>
        <span>{{ t('voicesName') }}</span>
        <input v-model.trim="label" type="text" required spellcheck="false" :disabled="busy" />
      </label>
      <label class="file">
        <span>{{ t('voicesFile') }}</span>
        <input
          ref="fileInput"
          type="file"
          accept="audio/*,.wav,.mp3,.flac,.m4a,.aac,.ogg,.opus"
          required
          :disabled="busy"
          @change="chosen"
        />
      </label>
      <button type="submit" class="button primary small" :disabled="busy || !label || !file">
        {{ busy ? t('voicesAdding') : t('voicesAdd') }}
      </button>
    </form>

    <div class="choices">
      <button type="button" class="button secondary" @click="close">{{ t('voicesClose') }}</button>
    </div>
  </dialog>
</template>

<style scoped>
.voice-collection {
  width: min(42rem, calc(100vw - 2rem));
  padding: 1.25rem;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: var(--surface);
  color: var(--text);
}

.voice-collection::backdrop {
  background: rgb(0 0 0 / 50%);
}

h3 {
  margin: 0;
  font-size: 1.1rem;
}

.intro {
  margin: 0.35rem 0 1rem;
  color: var(--text-muted);
  font-size: 0.9rem;
}

.hint {
  margin: 0 0 0.75rem;
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
  padding: 0.4rem 0.5rem 0.4rem 0;
  border-top: 1px solid var(--border);
  vertical-align: top;
  font-size: 0.9rem;
}

.audio {
  font-size: 0.8rem;
}

.original {
  display: block;
  color: var(--text-muted);
}

.time {
  white-space: nowrap;
}

.actions {
  text-align: right;
  white-space: nowrap;
}

/* Two buttons per voice; they need to stay two words, not one. */
.actions .link + .link {
  margin-left: 0.6rem;
}

.add {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 0.75rem;
  padding-top: 1rem;
  border-top: 1px solid var(--border);
}

.add label {
  display: grid;
  gap: 0.25rem;
  color: var(--text-muted);
  font-size: 0.8125rem;
}

.add .file {
  flex: 1 1 16rem;
  min-width: 0;
}

.add input[type='file'] {
  max-width: 100%;
  font-size: 0.8rem;
}

.empty {
  margin: 0 0 1rem;
  font-size: 0.9rem;
}

.choices {
  display: flex;
  justify-content: flex-end;
  margin-top: 1rem;
}

@media (max-width: 40rem) {
  /* Narrow screens do without the date; the voice and its recording matter there. */
  .time,
  th:nth-child(3) {
    display: none;
  }
}
</style>
