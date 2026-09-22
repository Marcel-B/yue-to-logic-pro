<script setup lang="ts">
import { computed, onUnmounted, ref, useTemplateRef } from 'vue'
import { ApiError, deleteStemJob, listStemJobs } from '../api'
import { formatDateTime, locale, t } from '../i18n'
import type { StemJob } from '../types'

/**
 * The stem service's own view: every job it knows, and a way to get rid of one. StemMyWav has no interface
 * of its own and takes only a couple of waiting jobs, so when it answers "queue full" this is where to see
 * what is holding it up - a job a browser tab started and forgot, one the Mac never finished - and to
 * cancel or remove it.
 */
const props = defineProps<{
  /** The job this session started or holds, so that it is marked and not removed by mistake. */
  ownJob: string | null
}>()

/** A job is gone from the service; the app then forgets it too if it was this session's. */
const emit = defineEmits<{ deleted: [id: string] }>()

/** The list is refreshed on its own while the dialog is open; the service's state changes by the minute, not the second. */
const refreshMilliseconds = 5000

const dialog = useTemplateRef<HTMLDialogElement>('dialog')
const jobs = ref<StemJob[]>([])
const loading = ref(false)
/** When the list was last fetched, so that a stale one can be told from a fresh one. */
const refreshedAt = ref<Date | null>(null)
const error = ref<string | null>(null)
/** The job being removed right now; its button is disabled meanwhile. */
const removing = ref<string | null>(null)
let timer: number | undefined
let pending: AbortController | null = null

const refreshedText = computed(() =>
  refreshedAt.value ? t('stemServiceRefreshed', { time: refreshedAt.value.toLocaleTimeString(locale.value) }) : '',
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
    jobs.value = await listStemJobs(controller.signal)
    refreshedAt.value = new Date()
    error.value = null
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

/** Cancels a waiting job or removes a finished one; the service says no while a job is on its way to the Mac. */
async function remove(job: StemJob): Promise<void> {
  const key = job.status === 'queued' ? 'stemServiceCancelConfirm' : 'stemServiceDeleteConfirm'
  if (removing.value || !window.confirm(t(key, { id: shortId(job.id) }))) {
    return
  }

  removing.value = job.id
  error.value = null
  try {
    await deleteStemJob(job.id)
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

function statusText(status: string): string {
  switch (status) {
    case 'queued':
      return t('stemServiceStatusQueued')
    case 'processing':
      return t('stemServiceStatusProcessing')
    case 'completed':
      return t('stemServiceStatusCompleted')
    case 'failed':
      return t('stemServiceStatusFailed')
    default:
      return status
  }
}

/** The first block of the UUID is enough to tell jobs apart in a list this short. */
function shortId(id: string): string {
  return id.split('-')[0] ?? id
}

function when(iso: string | null): string {
  return iso ? formatDateTime(iso) : '–'
}

function fail(caught: unknown): void {
  error.value =
    caught instanceof ApiError && caught.status === 0
      ? t('networkError')
      : t('stemServiceError', { message: caught instanceof Error ? caught.message : String(caught) })
}

defineExpose({ open })
</script>

<template>
  <dialog ref="dialog" class="stem-service-dialog" @cancel.prevent="close">
    <div class="head">
      <h3>{{ t('stemServiceTitle') }}</h3>
      <div class="refresh">
        <span class="muted">{{ loading ? t('stemServiceLoading') : refreshedText }}</span>
        <button type="button" class="button secondary small" :disabled="loading" @click="refresh">{{ t('stemServiceRefresh') }}</button>
      </div>
    </div>
    <p class="intro">{{ t('stemServiceIntro') }}</p>

    <p v-if="error" class="hint danger" role="alert">{{ error }}</p>

    <table v-if="jobs.length > 0">
      <thead>
        <tr>
          <th>{{ t('stemServiceJob') }}</th>
          <th>{{ t('stemServiceStatus') }}</th>
          <th>{{ t('stemServiceCreated') }}</th>
          <th>{{ t('stemServiceUpdated') }}</th>
          <th class="number">{{ t('stemServiceAttempts') }}</th>
          <th><span class="sr-only">{{ t('stemServiceDelete') }}</span></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="job in jobs" :key="job.id" :class="{ own: job.id === props.ownJob }">
          <td>
            <code :title="job.id">{{ shortId(job.id) }}</code>
            <span v-if="job.id === props.ownJob" class="own-mark">{{ t('stemServiceOwn') }}</span>
            <span v-if="job.lastError" class="error">{{ job.lastError }}</span>
          </td>
          <td><span class="status" :class="job.status">{{ statusText(job.status) }}</span></td>
          <td class="time">{{ when(job.createdUtc) }}</td>
          <td class="time">{{ when(job.updatedUtc) }}</td>
          <td class="number">{{ job.attempts }}</td>
          <td class="actions">
            <button
              v-if="job.status === 'processing'"
              type="button"
              class="link"
              disabled
              :title="t('stemServiceLocked')"
            >
              {{ t('stemServiceDelete') }}
            </button>
            <button v-else type="button" class="link" :disabled="removing !== null" @click="remove(job)">
              {{ job.status === 'queued' ? t('stemServiceCancel') : t('stemServiceDelete') }}
            </button>
          </td>
        </tr>
      </tbody>
    </table>
    <p v-else-if="!error" class="muted empty">{{ loading && !refreshedAt ? t('stemServiceLoading') : t('stemServiceEmpty') }}</p>

    <div class="choices">
      <button type="button" class="button secondary" @click="close">{{ t('stemServiceClose') }}</button>
    </div>
  </dialog>
</template>

<style scoped>
.stem-service-dialog {
  width: min(44rem, calc(100vw - 2rem));
  padding: 1.25rem;
  border: 1px solid var(--border);
  border-radius: var(--radius);
  background: var(--surface);
  color: var(--text);
}

.stem-service-dialog::backdrop {
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

/* One colour per state, so a full queue is seen before it is read. */
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

.status.processing {
  background: var(--accent-soft);
  color: var(--accent);
}

.status.failed {
  color: var(--danger);
}

.time {
  white-space: nowrap;
}

.number {
  text-align: right;
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
  /* Narrow screens do without the two timestamps; the status and the button are what matters there. */
  .time,
  th:nth-child(3),
  th:nth-child(4) {
    display: none;
  }
}
</style>
