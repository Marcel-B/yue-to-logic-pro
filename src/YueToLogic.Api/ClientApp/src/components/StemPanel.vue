<script setup lang="ts">
import { computed, onUnmounted, ref, useTemplateRef, watch } from 'vue'
import { ApiError, confirmStems, downloadStems, startStemJob, stemJobStatus } from '../api'
import { t } from '../i18n'
import { download } from '../score'

const props = defineProps<{
  audio: File | null
  /** File name of the conversion, so a downloaded ZIP is named after it. */
  outputName: string
}>()

/**
 * The finished job, which the Logic export then takes its stems from. It stays until the project has been
 * written - the stem service drops the files only once the import is confirmed.
 */
const job = defineModel<string | null>('job', { required: true })

/**
 * The job this panel is about, running or finished: the stem service dialog marks it as this session's,
 * so that it is not cancelled by mistake when the queue is being cleared.
 */
const tracked = defineModel<string | null>('tracked', { default: null })

/** Asks the app to write a Logic project now, which takes the stems along and confirms the import. */
const emit = defineEmits<{ exportLogic: [] }>()

/** How often the job is asked about; a separation runs for minutes, so this is not a busy wait. */
const pollMilliseconds = 5000

const dereverb = ref(false)
const status = ref<string | null>(null)
const error = ref<string | null>(null)
/** What happened to the job outside this panel, e.g. that it was removed in the stem service dialog. */
const note = ref<string | null>(null)
/** Whether a separation is running, which the result view says next to its Logic button. */
const busy = defineModel<boolean>('running', { required: true })
const downloading = ref(false)
const dialog = useTemplateRef<HTMLDialogElement>('dialog')
let jobId: string | null = null
let timer: number | undefined

const statusText = computed(() => {
  switch (status.value) {
    case 'queued':
      return t('stemsStatusQueued')
    case 'processing':
      return t('stemsStatusProcessing')
    default:
      return null
  }
})

/** A recording that is no longer the one the job was started for makes its result meaningless. */
watch(
  () => props.audio,
  () => reset(),
)

onUnmounted(() => window.clearTimeout(timer))

function reset(): void {
  window.clearTimeout(timer)
  // A job nobody is going to import any more: let the service drop its files now rather than in a day.
  if (job.value) {
    void confirmStems(job.value)
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
 * The job was removed in the stem service dialog, so there is nothing left to poll or to export: the panel
 * stops and says so, instead of running into the service's "unknown job" on the next poll.
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
  note.value = t('stemsRemovedElsewhere')
}

async function start(): Promise<void> {
  if (!props.audio) {
    return
  }

  reset()
  busy.value = true
  try {
    const started = await startStemJob(props.audio, dereverb.value)
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
      const state = await stemJobStatus(jobId)
      status.value = state.status
      if (state.status === 'completed') {
        // Nothing is downloaded on its own: the stems stay at the service until they are asked for.
        job.value = jobId
        jobId = null
        busy.value = false
        dialog.value?.showModal()
      } else if (state.status === 'failed') {
        error.value = state.lastError ?? t('stemsFailed')
        busy.value = false
      } else {
        poll()
      }
    } catch (caught) {
      fail(caught)
    }
  }, pollMilliseconds)
}

/** Writes the project with the stems in it; the server confirms the import, which removes them. */
function intoProject(): void {
  close()
  emit('exportLogic')
}

/** The stems as the service sends them, for whoever wants them beside the project. */
async function saveZip(): Promise<void> {
  if (!job.value) {
    return
  }

  downloading.value = true
  try {
    download(await downloadStems(job.value), `${props.outputName || 'score'}-stems.zip`)
  } catch (caught) {
    fail(caught)
  } finally {
    downloading.value = false
  }
}

/** Throws the result away, for a separation that turned out not to be worth keeping. */
function discard(): void {
  close()
  if (job.value) {
    void confirmStems(job.value)
    job.value = null
  }
  tracked.value = null
  status.value = null
}

function close(): void {
  dialog.value?.close()
}

function fail(caught: unknown): void {
  error.value = caught instanceof ApiError && caught.status === 0 ? t('networkError') : `${caught}`
  busy.value = false
  window.clearTimeout(timer)
}

defineExpose({ forget })
</script>

<template>
  <div class="stems">
    <h3>{{ t('stemsTitle') }}</h3>
    <p class="muted intro">{{ t('stemsInfo') }}</p>

    <div class="row">
      <button type="button" class="button secondary small" :disabled="!audio || busy" @click="start">
        {{ busy ? t('stemsRunning') : t('stemsStart') }}
      </button>
      <label class="check" :class="{ disabled: busy }">
        <input v-model="dereverb" type="checkbox" :disabled="busy" />
        {{ t('stemsDereverb') }}
      </label>
    </div>

    <div v-if="job" class="row ready">
      <button type="button" class="button secondary small" :disabled="downloading" @click="saveZip">
        {{ downloading ? t('stemsDownloading') : t('stemsDownload') }}
      </button>
      <button type="button" class="button secondary small" @click="discard">{{ t('stemsDiscard') }}</button>
    </div>

    <p v-if="!audio" class="hint muted">{{ t('stemsNeedsAudio') }}</p>
    <p v-else-if="busy && statusText" class="hint">{{ statusText }}</p>
    <p v-else-if="job" class="hint">{{ t('stemsWaiting') }}</p>
    <p v-else-if="note" class="hint muted">{{ note }}</p>
    <p v-if="error" class="hint danger" role="alert">{{ error }}</p>

    <dialog ref="dialog" class="stem-dialog" @cancel.prevent="close">
      <h3>{{ t('stemsReadyTitle') }}</h3>
      <p>{{ t('stemsReadyInfo') }}</p>
      <div class="choices">
        <button type="button" class="button primary" @click="intoProject">{{ t('stemsIntoProject') }}</button>
        <button type="button" class="button secondary" @click="close">{{ t('stemsLater') }}</button>
        <button type="button" class="button secondary" @click="discard">{{ t('stemsDiscard') }}</button>
      </div>
    </dialog>
  </div>
</template>

<style scoped>
.stems {
  margin-top: 1.5rem;
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

.check {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
}

.check.disabled {
  color: var(--text-muted);
}

.stem-dialog {
  max-width: 26rem;
  padding: 1.25rem;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: var(--surface);
  color: var(--text);
}

.stem-dialog::backdrop {
  background: rgb(0 0 0 / 50%);
}

.stem-dialog p {
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
