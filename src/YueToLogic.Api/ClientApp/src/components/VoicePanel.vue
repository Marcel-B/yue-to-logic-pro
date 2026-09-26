<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import { ApiError, downloadVoiceResult, forgetVoiceJob, startVoiceJob, voiceJobStatus, type VoiceSource } from '../api'
import { t } from '../i18n'
import { baseName, download } from '../score'
import type { ReferenceVoice } from '../types'
import FileDropZone from './FileDropZone.vue'

const props = defineProps<{
  /** The collection to choose from; kept by the app, since the dialog edits the same list. */
  voices: ReferenceVoice[]
  /**
   * The finished separation whose vocals get the new voice. Only the ids travel through the browser: the
   * vocal stem goes from the stem service to this server and on to the voice service.
   */
  stemJob: string | null
  /** Whether this server separates stems at all; without it a WAV of one's own is the only source. */
  stems: boolean
  /**
   * A new recording makes a conversion of its separated vocals meaningless, so the panel starts over with it.
   * A WAV of one's own is not tied to it and stays.
   */
  audio: File | null
  /** Whether a Logic project can be written now, which needs a score; without one the result is downloaded. */
  canExport: boolean
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
/**
 * Where the vocals come from. The separated ones are the default where there can be any, since that is the
 * way from a YuE song to its project; a WAV of one's own skips the separation.
 */
const source = ref<'stems' | 'file'>(props.stems ? 'stems' : 'file')
/** The vocals brought along, for the source 'file'. */
const ownVocals = ref<File | null>(null)
const status = ref<string | null>(null)
const error = ref<string | null>(null)
/** What happened to the job outside this panel, e.g. that it was removed in the job dialog. */
const note = ref<string | null>(null)
const downloading = ref(false)
/** Whether the dialog that asks what to do with a finished conversion is open. */
const ready = ref(false)
let jobId: string | null = null
let timer: number | undefined

/** The two sources as SelectButton options; a pair of radio buttons before, the same choice. */
const sources = computed(() => [
  { label: t('voiceSourceStems'), value: 'stems' },
  { label: t('voiceSourceFile'), value: 'file' },
])

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
  () => {
    if (source.value === 'stems') {
      reset()
    }
  },
)

watch(
  () => props.stems,
  (available) => {
    if (!available) {
      source.value = 'file'
    }
  },
)

/** What a start would send, or null while the chosen source has nothing to give. */
const vocals = computed<VoiceSource | null>(() => {
  if (source.value === 'file') {
    return ownVocals.value ? { file: ownVocals.value } : null
  }
  return props.stemJob ? { stemJob: props.stemJob } : null
})

/** Another WAV is another conversion; whatever was made from the last one goes. */
function selectVocals(file: File | null): void {
  reset()
  ownVocals.value = file
}

onUnmounted(() => window.clearTimeout(timer))

function reset(): void {
  window.clearTimeout(timer)
  // Everything this panel still holds at the service goes: a finished result nobody is going to import, and
  // a conversion that is still running. The running one matters most - it is only known here, so dropping it
  // silently would leave its files on the service until the cleanup run, and the Mac would compute a result
  // that nobody ever collects.
  const pending = job.value ?? jobId
  if (pending) {
    void forgetVoiceJob(pending)
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
 * The export could not take the result - vocals of another sample rate, an incomplete transfer - so the job
 * stays at the service and here. The hint below would otherwise promise the next project a voice that the
 * next project will refuse again; instead it points at the download, which is the way out by hand.
 */
function notTaken(): void {
  note.value = t('voiceNotTaken')
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
  if (!vocals.value || !voice.value) {
    return
  }

  const from = vocals.value
  reset()
  busy.value = true
  try {
    const started = await startVoiceJob(from, voice.value)
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
          ready.value = true
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

/**
 * The converted vocals as the service sends them, for whoever wants them beside the project or has no
 * project at all. A WAV of one's own gives its name to the result, so the two lie side by side.
 */
async function saveWav(): Promise<void> {
  if (!job.value) {
    return
  }

  const name =
    source.value === 'file' && ownVocals.value
      ? `${baseName(ownVocals.value.name)}-voice`
      : `${props.outputName || 'score'}-vocals`
  downloading.value = true
  try {
    download(await downloadVoiceResult(job.value), `${name}.wav`)
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
  // Nothing is left to download, so a note pointing at the download would be wrong.
  note.value = null
}

function close(): void {
  ready.value = false
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

/** Back to a fresh start, with the WAV of one's own gone as well; for the app's reset. */
function clear(): void {
  reset()
  ownVocals.value = null
}

defineExpose({ forget, notTaken, clear })
</script>

<template>
  <div class="mt-5 border-t border-surface pt-5">
    <h3 class="mt-0 mb-1 text-base font-semibold">{{ t('voiceTitle') }}</h3>
    <p class="muted mt-0 mb-3 text-sm">{{ t('voiceInfo') }}</p>

    <SelectButton
      v-if="stems"
      v-model="source"
      class="mb-3"
      :options="sources"
      option-label="label"
      option-value="value"
      :allow-empty="false"
      :disabled="busy"
      :aria-label="t('voiceSource')"
    />

    <div v-if="source === 'file'" class="mb-3">
      <FileDropZone
        :file="ownVocals"
        extension=".wav"
        accept=".wav,audio/wav,audio/x-wav,audio/wave"
        :drop-hint="t('voiceDropHint')"
        :wrong-type-hint="t('notWav')"
        @select="selectVocals"
        @clear="selectVocals(null)"
      />
    </div>

    <div v-if="voices.length > 0" class="mb-3 flex flex-wrap items-center gap-2 text-sm">
      <label for="voice-choice">{{ t('voiceChoose') }}</label>
      <Select
        v-model="voice"
        input-id="voice-choice"
        class="w-full max-w-full sm:w-56"
        :options="voices"
        option-label="label"
        option-value="id"
        :disabled="busy"
      />
    </div>

    <div class="flex flex-wrap items-center gap-3">
      <Button
        size="small"
        severity="secondary"
        outlined
        :label="busy ? t('voiceRunning') : t('voiceStart')"
        :loading="busy"
        :disabled="!vocals || !voice || busy"
        @click="start"
      />
    </div>

    <div v-if="job" class="mt-2 flex flex-wrap items-center gap-3">
      <Button
        size="small"
        severity="secondary"
        outlined
        :label="downloading ? t('voiceDownloading') : t('voiceDownload')"
        :loading="downloading"
        :disabled="downloading"
        @click="saveWav"
      />
      <Button size="small" severity="secondary" outlined :label="t('voiceDiscard')" @click="discard" />
    </div>

    <p v-if="voices.length === 0" class="hint muted">{{ t('voiceNoVoices') }}</p>
    <p v-else-if="!vocals && !job && !busy" class="hint muted">
      {{ source === 'file' ? t('voiceNeedsFile') : t('voiceNeedsStems') }}
    </p>
    <p v-if="busy && statusText" class="hint">{{ statusText }}</p>
    <p v-else-if="note" class="hint muted">{{ note }}</p>
    <p v-else-if="job" class="hint">{{ canExport ? t('voiceWaiting') : t('voiceWaitingNoProject') }}</p>
    <p v-if="error" class="hint danger" role="alert">{{ error }}</p>

    <Dialog
      v-model:visible="ready"
      modal
      :header="t('voiceReadyTitle')"
      :style="{ width: 'min(26rem, calc(100vw - 2rem))' }"
    >
      <p class="muted m-0 text-sm">{{ canExport ? t('voiceReadyInfo') : t('voiceReadyInfoNoProject') }}</p>
      <template #footer>
        <div class="flex flex-wrap justify-end gap-2">
          <Button v-if="canExport" :label="t('voiceIntoProject')" @click="intoProject" />
          <Button
            v-else
            :label="downloading ? t('voiceDownloading') : t('voiceDownload')"
            :loading="downloading"
            :disabled="downloading"
            @click="saveWav().then(close)"
          />
          <Button severity="secondary" outlined :label="t('voiceLater')" @click="close" />
          <Button severity="secondary" outlined :label="t('voiceDiscard')" @click="discard" />
        </div>
      </template>
    </Dialog>
  </div>
</template>
