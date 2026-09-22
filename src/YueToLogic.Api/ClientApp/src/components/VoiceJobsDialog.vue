<script setup lang="ts">
import { computed, onUnmounted, ref, useTemplateRef } from 'vue'
import { ApiError, deleteVoiceJob, listVoiceJobs } from '../api'
import { formatDateTime, locale, t } from '../i18n'
import type { VoiceJob } from '../types'

/**
 * The voice service's own view: every job it knows, and a way to get rid of one. ChangeMyVoice has no
 * interface of its own, so this is where a job that was started and forgotten is found and removed, and where
 * a failed one says why. A service that cannot list its jobs is not an error - the hint then says so.
 */
const props = defineProps<{
  /** The job this session started or holds, so that it is marked and not removed by mistake. */
  ownJob: string | null
}>()

/** A job is gone from the service; the app then forgets it too if it was this session's. */
const emit = defineEmits<{ deleted: [id: string] }>()

/** The list is refreshed on its own while the dialog is open; the state changes by the minute, not the second. */
const refreshMilliseconds = 5000

const dialog = useTemplateRef<HTMLDialogElement>('dialog')
const jobs = ref<VoiceJob[]>([])
const loading = ref(false)
/** When the list was last fetched, so that a stale one can be told from a fresh one. */
const refreshedAt = ref<Date | null>(null)
const error = ref<string | null>(null)
/** The service has no route for all of its jobs; that is a limit to explain, not a failure to report. */
const unsupported = ref(false)
/** The job being removed right now; its button is disabled meanwhile. */
const removing = ref<string | null>(null)
let timer: number | undefined
let pending: AbortController | null = null

const refreshedText = computed(() =>
  refreshedAt.value ? t('voiceJobsRefreshed', { time: refreshedAt.value.toLocaleTimeString(locale.value) }) : '',
)

onUnmounted(() => stop())

async function open(): Promise<void> {
  error.value = null
  dialog.value?.showModal()
  await refresh()
  schedule()
}

function close(): void {
  stop()
  dialog.value?.close()
}

function schedule(): void {
  window.clearTimeout(timer)
  if (unsupported.value) {
    return
  }

  timer = window.setTimeout(async () => {
    if (dialog.value?.open) {
      await refresh()
      schedule()
    }
  }, refreshMilliseconds)
}

function stop(): void {
  window.clearTimeout(timer)
  pending?.abort()
  pending = null
  loading.value = false
}

async function refresh(): Promise<void> {
  pending?.abort()
  const controller = new AbortController()
  pending = controller
  loading.value = true
  try {
    jobs.value = await listVoiceJobs(controller.signal)
    refreshedAt.value = new Date()
    error.value = null
    unsupported.value = false
  } catch (caught) {
    if (caught instanceof DOMException && caught.name === 'AbortError') {
      return
    }
    fail(caught)
  } finally {
    if (pending === controller) {
      loading.value = false
      pending = null
    }
  }
}

/** Cancels a waiting job or removes a finished one. */
async function remove(job: VoiceJob): Promise<void> {
  const key = status(job) === 'QUEUED' || status(job) === 'RUNNING' ? 'voiceJobsCancelConfirm' : 'voiceJobsDeleteConfirm'
  if (removing.value || !window.confirm(t(key, { id: shortId(job.id) }))) {
    return
  }

  removing.value = job.id
  error.value = null
  try {
    await deleteVoiceJob(job.id)
    emit('deleted', job.id)
  } catch (caught) {
    // Gone already is what was wanted; the refresh below shows it.
    if (!(caught instanceof ApiError && caught.status === 404)) {
      fail(caught)
    }
  } finally {
    removing.value = null
  }
  await refresh()
}

function status(job: VoiceJob): string {
  return job.status.toUpperCase()
}

function statusText(job: VoiceJob): string {
  switch (status(job)) {
    case 'QUEUED':
      return t('voiceJobsStatusQueued')
    case 'RUNNING':
      return t('voiceJobsStatusRunning')
    case 'COMPLETED':
      return t('voiceJobsStatusCompleted')
    case 'FAILED':
      return t('voiceJobsStatusFailed')
    case 'CANCELLED':
      return t('voiceJobsStatusCancelled')
    default:
      return job.status
  }
}

/** The first block of the id is enough to tell jobs apart in a list this short. */
function shortId(id: string): string {
  return id.split('-')[0] ?? id
}

function when(iso: string | null): string {
  return iso ? formatDateTime(iso) : '–'
}

function fail(caught: unknown): void {
  if (caught instanceof ApiError && caught.status === 501) {
    unsupported.value = true
    jobs.value = []
    error.value = null
    return
  }

  error.value =
    caught instanceof ApiError && caught.status === 0
      ? t('networkError')
      : t('voiceJobsError', { message: caught instanceof Error ? caught.message : String(caught) })
}

defineExpose({ open })
</script>

<template>
  <dialog ref="dialog" class="voice-jobs" @cancel.prevent="close">
    <div class="head">
      <h3>{{ t('voiceJobsTitle') }}</h3>
      <div v-if="!unsupported" class="refresh">
        <span class="muted">{{ loading ? t('voiceJobsLoading') : refreshedText }}</span>
        <button type="button" class="button secondary small" :disabled="loading" @click="refresh">{{ t('voiceJobsRefresh') }}</button>
      </div>
    </div>
    <p class="intro">{{ t('voiceJobsIntro') }}</p>

    <p v-if="error" class="hint danger" role="alert">{{ error }}</p>
    <p v-if="unsupported" class="hint muted">{{ t('voiceJobsUnsupported') }}</p>

    <table v-if="jobs.length > 0">
      <thead>
        <tr>
          <th>{{ t('voiceJobsJob') }}</th>
          <th>{{ t('voiceJobsVoice') }}</th>
          <th>{{ t('voiceJobsStatus') }}</th>
          <th>{{ t('voiceJobsCreated') }}</th>
          <th>{{ t('voiceJobsFinished') }}</th>
          <th><span class="sr-only">{{ t('voiceJobsDelete') }}</span></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="job in jobs" :key="job.id" :class="{ own: job.id === props.ownJob }">
          <td>
            <code :title="job.id">{{ shortId(job.id) }}</code>
            <span v-if="job.id === props.ownJob" class="own-mark">{{ t('voiceJobsOwn') }}</span>
            <span v-if="job.errorMessage" class="error">{{ job.errorMessage }}</span>
          </td>
          <td class="voice">{{ job.voiceLabel ?? job.voiceId ?? '–' }}</td>
          <td><span class="status" :class="status(job).toLowerCase()">{{ statusText(job) }}</span></td>
          <td class="time">{{ when(job.createdUtc) }}</td>
          <td class="time">{{ when(job.finishedUtc) }}</td>
          <td class="actions">
            <button type="button" class="link" :disabled="removing !== null" @click="remove(job)">
              {{ status(job) === 'QUEUED' || status(job) === 'RUNNING' ? t('voiceJobsCancel') : t('voiceJobsDelete') }}
            </button>
          </td>
        </tr>
      </tbody>
    </table>
    <p v-else-if="!error && !unsupported" class="muted empty">
      {{ loading && !refreshedAt ? t('voiceJobsLoading') : t('voiceJobsEmpty') }}
    </p>

    <div class="choices">
      <button type="button" class="button secondary" @click="close">{{ t('voiceJobsClose') }}</button>
    </div>
  </dialog>
</template>

<style scoped>
.voice-jobs {
  width: min(44rem, calc(100vw - 2rem));
  padding: 1.25rem;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: var(--surface);
  color: var(--text);
}

.voice-jobs::backdrop {
  background: rgb(0 0 0 / 50%);
}

.head {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem 1rem;
}

h3 {
  margin: 0;
  font-size: 1.1rem;
}

.refresh {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  font-size: 0.8rem;
}

.button.small {
  padding: 0.3rem 0.75rem;
  font-size: 0.8rem;
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

tr.own td {
  background: var(--accent-soft);
}

code {
  font-family: var(--font-mono);
  font-size: 0.85rem;
}

.voice {
  font-size: 0.85rem;
}

.own-mark {
  display: block;
  color: var(--text-muted);
  font-size: 0.75rem;
}

.error {
  display: block;
  max-width: 24rem;
  color: var(--danger);
  font-size: 0.8rem;
}

/* One colour per state, so a queue that is not moving is seen before it is read. */
.status {
  display: inline-block;
  padding: 0.1rem 0.5rem;
  border-radius: 999px;
  background: var(--surface-sunken);
  font-size: 0.8rem;
  white-space: nowrap;
}

.status.queued {
  color: var(--warning-text);
}

.status.running {
  background: var(--accent-soft);
  color: var(--accent);
}

.status.failed {
  color: var(--danger);
}

.time {
  white-space: nowrap;
}

.actions {
  text-align: right;
  white-space: nowrap;
}

.link:disabled {
  color: var(--text-muted);
  cursor: not-allowed;
  text-decoration: none;
}

.empty {
  margin: 0 0 1rem;
  font-size: 0.9rem;
}

.choices {
  display: flex;
  justify-content: flex-end;
}

@media (max-width: 40rem) {
  /* Narrow screens do without the voice and the two timestamps; the status and the button matter there. */
  .time,
  .voice,
  th:nth-child(2),
  th:nth-child(4),
  th:nth-child(5) {
    display: none;
  }
}
</style>
