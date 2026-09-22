<script setup lang="ts">
import { computed, onUnmounted, ref, useTemplateRef, watch } from 'vue'
import { ApiError, downloadVoiceResult, forgetVoiceJob, startVoiceJob, voiceJobStatus } from '../api'
import { t } from '../i18n'
import { download } from '../score'
import type { ReferenceVoice } from '../types'

const props = defineProps<{
  /** The collection to choose from; kept by the app, since the dialog edits the same list. */
  voices: ReferenceVoice[]
  /**
   * The finished separation whose vocals get the new voice. Only the ids travel through the browser: the
   * vocal stem goes from the stem service to this server and on to the voice service.
   */
  stemJob: string | null
  /** A new recording makes a running conversion meaningless, so the panel starts over with it. */
  audio: File | null
  /** File name of the conversion, so a downloaded WAV is named after it. */
  outputName: string
}>()

/**
 * The finished job, whose vocals the Logic export then takes. It stays until the project has been written -
 * the voice service drops the result only once the import is confirmed.
 */
const job = defineModel<string | null>('job', { required: true })

/**
 * The job this panel is about, running or finished: the job dialog marks it as this session's, so that it is
 * not cancelled by mistake when the list is being cleared.
 */
const tracked = defineModel<string | null>('tracked', { default: null })

/** Whether a conversion is running, which the result view says next to its Logic button. */
const busy = defineModel<boolean>('running', { required: true })

/** Asks the app to write a Logic project now, which takes the new vocals along and confirms the import. */
const emit = defineEmits<{ exportLogic: [] }>()

/** How often the job is asked about; a conversion runs for minutes, so this is not a busy wait. */
const pollMilliseconds = 5000

const storageKey = 'yue-to-logic.voice'

/** The voice of the last conversion, so that a choice that works is not made again every time. */
function storedVoice(): string {
  try {
    return localStorage.getItem(storageKey) ?? ''
  } catch {
    return ''
  }
}

const voice = ref(storedVoice())
const status = ref<string | null>(null)
const error = ref<string | null>(null)
/** What happened to the job outside this panel, e.g. that it was removed in the job dialog. */
const note = ref<string | null>(null)
const downloading = ref(false)
const dialog = useTemplateRef<HTMLDialogElement>('dialog')
let jobId: string | null = null
let timer: number | undefined

const statusText = computed(() => {
  switch (status.value?.toUpperCase()) {
    case 'QUEUED':
      return t('voiceStatusQueued')
    case 'RUNNING':
      return t('voiceStatusRunning')
    default:
      return null
  }
})

// A voice that is no longer in the collection cannot be chosen; the first one then stands ready instead.
watch(
  () => props.voices,
  (list) => {
    if (list.length > 0 && !list.some((entry) => entry.id === voice.value)) {
      voice.value = list[0]!.id
    }
  },
  { immediate: true },
)

watch(voice, (id) => {
  try {
    localStorage.setItem(storageKey, id)
  } catch {
    // Remembering the last voice is a convenience only.
  }
})

watch(
  () => props.audio,
  () => reset(),
)

onUnmounted(() => window.clearTimeout(timer))

function reset(): void {
  window.clearTimeout(timer)
  // A result nobody is going to import any more: let the service drop it now rather than after its retention.
  if (job.value) {
    void forgetVoiceJob(job.value)
  }
  close()
  jobId = null
  job.value = null
  tracked.value = null
  status.value = null
  error.value = null
  note.value = null
  busy.value = false
}

/**
 * The job was removed in the job dialog, so there is nothing left to poll or to export: the panel stops and
 * says so, instead of running into the service's "unknown job" on the next poll.
 */
function forget(id: string): void {
  if (id !== tracked.value) {
    return
  }

  window.clearTimeout(timer)
  jobId = null
  job.value = null
  tracked.value = null
  status.value = null
  error.value = null
  busy.value = false
  note.value = t('voiceRemovedElsewhere')
}

async function start(): Promise<void> {
  if (!props.stemJob || !voice.value) {
    return
  }

  const separation = props.stemJob
  reset()
  busy.value = true
  try {
    const started = await startVoiceJob(separation, voice.value)
    jobId = started.id
    tracked.value = started.id
    status.value = started.status
    poll()
  } catch (caught) {
    fail(caught)
  }
}

function poll(): void {
  timer = window.setTimeout(async () => {
    if (!jobId) {
      return
    }

    try {
      const state = await voiceJobStatus(jobId)
      status.value = state.status
      switch (state.status.toUpperCase()) {
        case 'COMPLETED':
          // Nothing is downloaded on its own: the result stays at the service until it is asked for.
          job.value = jobId
          jobId = null
          busy.value = false
          dialog.value?.showModal()
          break
        case 'FAILED':
          error.value = state.errorMessage ?? t('voiceFailed')
          busy.value = false
          break
        case 'CANCELLED':
          note.value = t('voiceRemovedElsewhere')
          busy.value = false
          break
        default:
          poll()
      }
    } catch (caught) {
      fail(caught)
    }
  }, pollMilliseconds)
}

/** Writes the project with the new vocals in it; the server confirms the import, which removes them. */
function intoProject(): void {
  close()
  emit('exportLogic')
}

/** The converted vocals as the service sends them, for whoever wants them beside the project. */
async function saveWav(): Promise<void> {
  if (!job.value) {
    return
  }

  downloading.value = true
  try {
    download(await downloadVoiceResult(job.value), `${props.outputName || 'score'}-vocals.wav`)
  } catch (caught) {
    fail(caught)
  } finally {
    downloading.value = false
  }
}

/** Throws the result away, for a conversion that turned out not to be worth keeping. */
function discard(): void {
  close()
  if (job.value) {
    void forgetVoiceJob(job.value)
    job.value = null
  }
  tracked.value = null
  status.value = null
}

function close(): void {
  dialog.value?.close()
}

function fail(caught: unknown): void {
  if (caught instanceof ApiError && caught.status === 0) {
    error.value = t('networkError')
  } else if (caught instanceof ApiError && (caught.status === 429 || caught.status === 503)) {
    // A rate limit or a full queue: the same request is worth repeating later, unlike a refused recording.
    error.value = t('voiceRetryLater', { message: caught.message })
  } else {
    error.value = caught instanceof Error ? caught.message : String(caught)
  }
  busy.value = false
  window.clearTimeout(timer)
}

defineExpose({ forget })
</script>

<template>
  <div class="voice">
    <h3>{{ t('voiceTitle') }}</h3>
    <p class="muted intro">{{ t('voiceInfo') }}</p>

    <label v-if="voices.length > 0" class="field">
      <span>{{ t('voiceChoose') }}</span>
      <select v-model="voice" :disabled="busy">
        <option v-for="option in voices" :key="option.id" :value="option.id">{{ option.label }}</option>
      </select>
    </label>

    <div class="row">
      <button type="button" class="button secondary small" :disabled="!stemJob || !voice || busy" @click="start">
        {{ busy ? t('voiceRunning') : t('voiceStart') }}
      </button>
    </div>

    <div v-if="job" class="row ready">
      <button type="button" class="button secondary small" :disabled="downloading" @click="saveWav">
        {{ downloading ? t('voiceDownloading') : t('voiceDownload') }}
      </button>
      <button type="button" class="button secondary small" @click="discard">{{ t('voiceDiscard') }}</button>
    </div>

    <p v-if="voices.length === 0" class="hint muted">{{ t('voiceNoVoices') }}</p>
    <p v-else-if="!stemJob && !job && !busy" class="hint muted">{{ t('voiceNeedsStems') }}</p>
    <p v-if="busy && statusText" class="hint">{{ statusText }}</p>
    <p v-else-if="job" class="hint">{{ t('voiceWaiting') }}</p>
    <p v-else-if="note" class="hint muted">{{ note }}</p>
    <p v-if="error" class="hint danger" role="alert">{{ error }}</p>

    <dialog ref="dialog" class="voice-dialog" @cancel.prevent="close">
      <h3>{{ t('voiceReadyTitle') }}</h3>
      <p>{{ t('voiceReadyInfo') }}</p>
      <div class="choices">
        <button type="button" class="button primary" @click="intoProject">{{ t('voiceIntoProject') }}</button>
        <button type="button" class="button secondary" @click="close">{{ t('voiceLater') }}</button>
        <button type="button" class="button secondary" @click="discard">{{ t('voiceDiscard') }}</button>
      </div>
    </dialog>
  </div>
</template>

<style scoped>
.voice {
  margin-top: 1.25rem;
  padding-top: 1.25rem;
  border-top: 1px solid var(--border);
}

h3 {
  margin: 0 0 0.35rem;
  font-size: 1rem;
}

.row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.75rem;
}

.row.ready {
  margin-top: 0.5rem;
}

.field {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
  font-size: 0.9rem;
}

.field select {
  min-width: 14rem;
  max-width: 100%;
}

.voice-dialog {
  max-width: 26rem;
  padding: 1.25rem;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: var(--surface);
  color: var(--text);
}

.voice-dialog::backdrop {
  background: rgb(0 0 0 / 50%);
}

.voice-dialog p {
  margin: 0 0 1rem;
  color: var(--text-muted);
  font-size: 0.9rem;
}

.choices {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
}
</style>
