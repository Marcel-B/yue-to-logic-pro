<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, useTemplateRef, watch } from 'vue'
import { ApiError, confirmStems, downloadStems, listStemModels, startStemJob, stemJobStatus } from '../api'
import { locale, t } from '../i18n'
import { download } from '../score'
import type { SeparationModel } from '../types'

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

const modelStorageKey = 'yue-to-logic.stemModel'

/** The model of the last separation, so that a setup that works is not chosen again every time. */
function storedModel(): string {
  try {
    return localStorage.getItem(modelStorageKey) ?? ''
  } catch {
    return ''
  }
}

const dereverb = ref(false)
/** What the service can separate with; empty when its list could not be fetched. */
const models = ref<SeparationModel[]>([])
/** The chosen model's id; empty means the service takes its own default. */
const model = ref(storedModel())
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

const chosen = computed(() => models.value.find((m) => m.id === model.value) ?? null)

/**
 * Only a model that separates a vocal stem can have its reverb taken off, and only such a model fills the
 * project's stem tracks; with any other the switch is pointless and the hint below the choice says so.
 */
const hasVocals = computed(() => chosen.value === null || chosen.value.stems.some((stem) => stem.startsWith('vocals')))

/**
 * The models by what they separate, in the order the service lists them. It offers a couple of dozen, so
 * without the grouping the list would be a wall of names that all begin alike.
 */
const grouped = computed(() => {
  const groups: { task: string; label: string; models: SeparationModel[] }[] = []
  for (const value of models.value) {
    const task = value.task ?? ''
    const group = groups.find((g) => g.task === task)
    if (group) {
      group.models.push(value)
    } else {
      groups.push({ task, label: taskText(task), models: [value] })
    }
  }
  return groups
})

/** What the chosen model does, in one line: which stems it returns, how long it computes, what it is known for. */
const modelText = computed(() => {
  const value = chosen.value
  if (!value) {
    return null
  }

  const parts = [
    value.stems.length > 0 ? t('stemsModelStems', { stems: value.stems.join(', ') }) : null,
    speedText(value.speed),
    factorText(value),
    value.notes,
  ]
  return parts.filter((part) => part).join(' · ')
})

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

// The list comes from the gateway itself, so it is there even while the separating Mac is not.
onMounted(async () => {
  try {
    models.value = await listStemModels()
    if (!models.value.some((m) => m.id === model.value)) {
      model.value = models.value.find((m) => m.isDefault)?.id ?? models.value[0]?.id ?? ''
    }
  } catch {
    // Without the list there is no choice to make; the service then separates with its own default.
    models.value = []
    model.value = ''
  }
})

// A model without a vocal stem leaves nothing for the dereverb to work on.
watch(hasVocals, (possible) => {
  if (!possible) {
    dereverb.value = false
  }
})

watch(model, (id) => {
  try {
    localStorage.setItem(modelStorageKey, id)
  } catch {
    // Remembering the last model is a convenience only.
  }
})

function reset(): void {
  window.clearTimeout(timer)
  // Everything this panel still holds at the service goes: a finished separation nobody is going to import,
  // and one that is still running. The running one matters most - it is only known here, so dropping it
  // silently would keep a place in the queue that the service only takes a couple of.
  const pending = job.value ?? jobId
  if (pending) {
    void confirmStems(pending)
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
    const started = await startStemJob(props.audio, dereverb.value, model.value)
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

function taskText(task: string): string {
  switch (task) {
    case 'vocals':
      return t('stemsModelTaskVocals')
    case 'instrumental':
      return t('stemsModelTaskInstrumental')
    case 'karaoke':
      return t('stemsModelTaskKaraoke')
    case '4stem':
      return t('stemsModelTask4Stem')
    case '6stem':
      return t('stemsModelTask6Stem')
    case 'drums':
      return t('stemsModelTaskDrums')
    default:
      return task || t('stemsModelTaskOther')
  }
}

function speedText(speed: string | null): string | null {
  switch (speed) {
    case 'fast':
      return t('stemsModelSpeedFast')
    case 'moderate':
      return t('stemsModelSpeedModerate')
    case 'slow':
      return t('stemsModelSpeedSlow')
    case 'verySlow':
      return t('stemsModelSpeedVerySlow')
    default:
      return null
  }
}

/**
 * The service reports audio length divided by computing time; turned around it says how many times the song's
 * own length the separation takes, which is what one waits for.
 */
function factorText(value: SeparationModel): string | null {
  if (!value.realtimeFactor || value.realtimeFactor <= 0) {
    return null
  }

  const times = (Math.round((1 / value.realtimeFactor) * 10) / 10).toLocaleString(locale.value)
  return t(value.measured ? 'stemsModelFactor' : 'stemsModelFactorEstimated', { times })
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

    <label v-if="models.length > 0" class="field">
      <span>{{ t('stemsModel') }}</span>
      <select v-model="model" :disabled="busy">
        <optgroup v-for="group in grouped" :key="group.task" :label="group.label">
          <option v-for="option in group.models" :key="option.id" :value="option.id">
            {{ option.isDefault ? t('stemsModelDefault', { name: option.name }) : option.name }}
          </option>
        </optgroup>
      </select>
    </label>
    <p v-if="modelText" class="hint muted model-info">{{ modelText }}</p>
    <p v-if="!hasVocals" class="hint muted">{{ t('stemsModelNoVocals') }}</p>

    <div class="row">
      <button type="button" class="button secondary small" :disabled="!audio || busy" @click="start">
        {{ busy ? t('stemsRunning') : t('stemsStart') }}
      </button>
      <label class="check" :class="{ disabled: busy || !hasVocals }">
        <input v-model="dereverb" type="checkbox" :disabled="busy || !hasVocals" />
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

.field {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
  font-size: 0.9rem;
}

.field select {
  min-width: 14rem;
  max-width: 100%;
}

.model-info {
  margin-bottom: 0.75rem;
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
