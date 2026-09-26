<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
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

const visible = ref(false)
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

// However the dialog is closed (button, Escape, the close icon), the refreshing stops with it.
watch(visible, (open) => {
  if (!open) {
    stop()
  }
})

async function open(): Promise<void> {
  error.value = null
  visible.value = true
  await refresh()
  schedule()
}

function close(): void {
  stop()
  visible.value = false
}

function schedule(): void {
  window.clearTimeout(timer)
  timer = window.setTimeout(async () => {
    if (visible.value) {
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

/** One colour per state, so a full queue is seen before it is read. */
function statusSeverity(status: string): string {
  switch (status) {
    case 'queued':
      return 'warn'
    case 'processing':
      return 'info'
    case 'failed':
      return 'danger'
    default:
      return 'secondary'
  }
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
  <Dialog
    v-model:visible="visible"
    modal
    :header="t('stemServiceTitle')"
    :draggable="false"
    :style="{ width: 'min(44rem, calc(100vw - 2rem))' }"
  >
    <div class="mb-3 flex flex-wrap items-center justify-between gap-2">
      <p class="muted m-0 text-sm">{{ t('stemServiceIntro') }}</p>
      <div class="flex items-center gap-2 text-xs">
        <span class="muted">{{ loading ? t('stemServiceLoading') : refreshedText }}</span>
        <Button
          :label="t('stemServiceRefresh')"
          icon="pi pi-refresh"
          :disabled="loading"
          severity="secondary"
          outlined
          size="small"
          @click="refresh"
        />
      </div>
    </div>

    <p v-if="error" class="hint danger mt-0 mb-3" role="alert">{{ error }}</p>

    <table v-if="jobs.length > 0">
      <thead>
        <tr>
          <th>{{ t('stemServiceJob') }}</th>
          <th>{{ t('stemServiceModel') }}</th>
          <th>{{ t('stemServiceStatus') }}</th>
          <th>{{ t('stemServiceCreated') }}</th>
          <th>{{ t('stemServiceUpdated') }}</th>
          <th class="number">{{ t('stemServiceAttempts') }}</th>
          <th>
            <span class="sr-only">{{ t('stemServiceDelete') }}</span>
          </th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="job in jobs" :key="job.id" :class="{ own: job.id === props.ownJob }">
          <td>
            <code :title="job.id">{{ shortId(job.id) }}</code>
            <span v-if="job.id === props.ownJob" class="muted block text-xs">{{ t('stemServiceOwn') }}</span>
            <span v-if="job.lastError" class="error block max-w-96 text-xs">{{ job.lastError }}</span>
          </td>
          <td class="model text-xs">{{ job.model ?? '–' }}</td>
          <td>
            <Tag
              :severity="statusSeverity(job.status)"
              :value="statusText(job.status)"
              rounded
              class="whitespace-nowrap"
            />
          </td>
          <td class="time">{{ when(job.createdUtc) }}</td>
          <td class="time">{{ when(job.updatedUtc) }}</td>
          <td class="number">{{ job.attempts }}</td>
          <td class="text-right whitespace-nowrap">
            <!-- A disabled button shows no tooltip of its own, so the wrapper carries why it is locked. -->
            <span v-if="job.status === 'processing'" v-tooltip.left="t('stemServiceLocked')" class="inline-block">
              <Button :label="t('stemServiceDelete')" link size="small" disabled />
            </span>
            <Button
              v-else
              :label="job.status === 'queued' ? t('stemServiceCancel') : t('stemServiceDelete')"
              link
              size="small"
              :disabled="removing !== null"
              :loading="removing === job.id"
              @click="remove(job)"
            />
          </td>
        </tr>
      </tbody>
    </table>
    <p v-else-if="!error" class="muted mt-0 mb-3 text-sm">
      {{ loading && !refreshedAt ? t('stemServiceLoading') : t('stemServiceEmpty') }}
    </p>

    <template #footer>
      <Button :label="t('stemServiceClose')" severity="secondary" outlined @click="close" />
    </template>
  </Dialog>
</template>

<style scoped>
table {
  width: 100%;
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

.error {
  color: var(--danger);
}

.time {
  white-space: nowrap;
}

.number {
  text-align: right;
}

@media (max-width: 40rem) {
  /* Narrow screens do without the model and the two timestamps; the status and the button matter there. */
  .time,
  .model,
  th:nth-child(2),
  th:nth-child(4),
  th:nth-child(5) {
    display: none;
  }
}
</style>
