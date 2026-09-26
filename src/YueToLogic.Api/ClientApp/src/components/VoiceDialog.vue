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

const visible = ref(false)
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
  visible.value = true
}

function close(): void {
  visible.value = false
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

  const channels =
    properties.channels === 1 ? 'mono' : properties.channels === 2 ? 'stereo' : `${properties.channels} ch`
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
        ? // A rate limit or a full queue: the same request is worth repeating later, unlike a refused recording.
          t('voiceRetryLater', { message: caught.message })
        : t('voicesError', { message: caught instanceof Error ? caught.message : String(caught) })
}

defineExpose({ open })
</script>

<template>
  <Dialog
    v-model:visible="visible"
    modal
    :header="t('voicesTitle')"
    :style="{ width: 'min(42rem, calc(100vw - 2rem))' }"
  >
    <p class="muted mt-0 mb-4 text-sm">{{ t('voicesIntro') }}</p>

    <p v-if="error" class="hint danger mt-0 mb-3" role="alert">{{ error }}</p>

    <table v-if="voices.length > 0" class="mb-4 w-full border-collapse text-sm">
      <thead>
        <tr class="text-left text-[0.8125rem] text-muted-color">
          <th class="py-1.5 pr-2 font-medium">{{ t('voicesVoice') }}</th>
          <th class="py-1.5 pr-2 font-medium">{{ t('voicesAudio') }}</th>
          <!-- Narrow screens do without the date; the voice and its recording matter there. -->
          <th class="py-1.5 pr-2 font-medium max-sm:hidden">{{ t('voicesCreated') }}</th>
          <th class="py-1.5">
            <span class="sr-only">{{ t('voicesActions') }}</span>
          </th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="entry in voices" :key="entry.id" class="border-t border-surface align-top">
          <td class="py-1.5 pr-2">{{ entry.label }}</td>
          <td class="py-1.5 pr-2 text-xs">
            {{ summary(entry.stored) }}
            <span v-if="entry.original" class="block text-muted-color">
              {{ t('voicesOriginal', { summary: summary(entry.original) }) }}
            </span>
          </td>
          <td class="py-1.5 pr-2 whitespace-nowrap max-sm:hidden">{{ when(entry.createdUtc) }}</td>
          <td class="py-0.5 text-right whitespace-nowrap">
            <Button
              link
              size="small"
              v-tooltip.top="t('voicesDownloadTitle')"
              :label="downloading === entry.id ? t('voicesDownloading') : t('voicesDownload')"
              :disabled="downloading !== null"
              @click="save(entry)"
            />
            <Button
              link
              size="small"
              severity="danger"
              :label="t('voicesDelete')"
              :disabled="removing !== null || busy"
              @click="remove(entry)"
            />
          </td>
        </tr>
      </tbody>
    </table>
    <p v-else class="muted mt-0 mb-4 text-sm">{{ t('voicesEmpty') }}</p>

    <form class="flex flex-wrap items-end gap-3 border-t border-surface pt-4" @submit.prevent="add">
      <div class="grid gap-1 text-[0.8125rem] text-muted-color">
        <label for="voice-name">{{ t('voicesName') }}</label>
        <InputText id="voice-name" v-model.trim="label" required spellcheck="false" :disabled="busy" />
      </div>
      <div class="grid min-w-0 flex-[1_1_16rem] gap-1 text-[0.8125rem] text-muted-color">
        <label for="voice-file">{{ t('voicesFile') }}</label>
        <!-- A native file input: PrimeVue's FileUpload brings its own upload flow, while this only picks a file. -->
        <input
          id="voice-file"
          ref="fileInput"
          class="max-w-full text-xs"
          type="file"
          accept="audio/*,.wav,.mp3,.flac,.m4a,.aac,.ogg,.opus"
          required
          :disabled="busy"
          @change="chosen"
        />
      </div>
      <Button
        type="submit"
        size="small"
        :label="busy ? t('voicesAdding') : t('voicesAdd')"
        :loading="busy"
        :disabled="busy || !label || !file"
      />
    </form>

    <template #footer>
      <Button severity="secondary" outlined :label="t('voicesClose')" @click="close" />
    </template>
  </Dialog>
</template>
