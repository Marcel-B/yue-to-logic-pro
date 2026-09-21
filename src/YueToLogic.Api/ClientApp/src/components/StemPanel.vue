<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import { ApiError, confirmStems, downloadStems, startStemJob, stemJobStatus } from '../api'
import { t } from '../i18n'
import { download } from '../score'

const props = defineProps<{
  audio: File | null
  /** File name of the conversion, so the stems land beside the MIDI file under the same name. */
  outputName: string
}>()

/** How often the job is asked about; a separation runs for minutes, so this is not a busy wait. */
const pollMilliseconds = 5000

const dereverb = ref(false)
const status = ref<string | null>(null)
const error = ref<string | null>(null)
const busy = ref(false)
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
  jobId = null
  status.value = null
  error.value = null
  busy.value = false
}

async function start(): Promise<void> {
  if (!props.audio) {
    return
  }

  reset()
  busy.value = true
  try {
    const job = await startStemJob(props.audio, dereverb.value)
    jobId = job.id
    status.value = job.status
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
      const job = await stemJobStatus(jobId)
      status.value = job.status
      if (job.status === 'completed') {
        await save(jobId)
      } else if (job.status === 'failed') {
        error.value = job.lastError ?? t('stemsFailed')
        busy.value = false
      } else {
        poll()
      }
    } catch (caught) {
      fail(caught)
    }
  }, pollMilliseconds)
}

/** Downloads the ZIP and tells the service it may drop the files. */
async function save(id: string): Promise<void> {
  const stems = await downloadStems(id)
  download(stems, `${props.outputName || 'score'}-stems.zip`)
  await confirmStems(id)
  jobId = null
  busy.value = false
}

function fail(caught: unknown): void {
  error.value = caught instanceof ApiError && caught.status === 0 ? t('networkError') : `${caught}`
  busy.value = false
  window.clearTimeout(timer)
}
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

    <p v-if="!audio" class="hint muted">{{ t('stemsNeedsAudio') }}</p>
    <p v-else-if="busy && statusText" class="hint">{{ statusText }}</p>
    <p v-else-if="status === 'completed'" class="hint">{{ t('stemsDone') }}</p>
    <p v-if="error" class="hint danger" role="alert">{{ error }}</p>
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

.check {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
}

.check.disabled {
  color: var(--text-muted);
}
</style>
