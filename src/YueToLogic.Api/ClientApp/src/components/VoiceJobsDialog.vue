<script setup lang="ts">
import { computed, onUnmounted, ref } from 'vue'
import { ApiError, deleteVoiceJob, downloadVoiceResult, listVoiceJobs } from '../api'
import { formatDateTime, locale, t } from '../i18n'
import { download } from '../score'
import type { VoiceJob } from '../types'

/**
 * The voice service's own view: the jobs it knows, and a way to get rid of one. ChangeMyVoice has no
 * interface of its own, so this is where a job that was started and forgotten is found and removed, and where
 * a failed one says why. A service that cannot list its jobs is not an error - the hint then says so.
 *
 * The service keeps a record of every job it ever had, so the list only grows: it is read page by page, and
 * a state can be picked to look for what is waiting or what failed.
 */
const props = defineProps<{
  /** The job this session started or holds, so that it is marked and not removed by mistake. */
  ownJob: string | null
}>()

/** A job is gone from the service; the app then forgets it too if it was this session's. */
const emit = defineEmits<{ deleted: [id: string] }>()

/** The list is refreshed on its own while the dialog is open; the state changes by the minute, not the second. */
const refreshMilliseconds = 5000

/** How many jobs a page holds; enough to see what is going on without scrolling the dialog away. */
const pageSize = 25

/** The states the service knows, in the order the filter offers them. */
const states = ['QUEUED', 'RUNNING', 'COMPLETED', 'FAILED', 'CANCELLED'] as const

const visible = ref(false)
const jobs = ref<VoiceJob[]>([])
/** How many jobs match the chosen state altogether, which is what the paging is measured against. */
const total = ref(0)
/** How many jobs the shown page skips. */
const offset = ref(0)
/** The state the list is narrowed to, or an empty string for all of them. */
const filter = ref('')
const loading = ref(false)
/** When the list was last fetched, so that a stale one can be told from a fresh one. */
const refreshedAt = ref<Date | null>(null)
const error = ref<string | null>(null)
/** The service has no route for all of its jobs; that is a limit to explain, not a failure to report. */
const unsupported = ref(false)
/** The job being removed right now; its button is disabled meanwhile. */
const removing = ref<string | null>(null)
/** The job whose result is being fetched right now; its button says so meanwhile. */
const fetching = ref<string | null>(null)
let timer: number | undefined
let pending: AbortController | null = null

/** The filter's choices: every state, and an empty value for all of them. */
const filterOptions = computed(() => [
  { label: t('voiceJobsFilterAll'), value: '' },
  ...states.map((state) => ({ label: statusText(state), value: state })),
])

/** One colour per state, so a queue that is not moving is seen before it is read. */
function severity(job: VoiceJob): string {
  switch (status(job)) {
    case 'QUEUED':
      return 'warn'
    case 'RUNNING':
      return 'info'
    case 'FAILED':
      return 'danger'
    default:
      return 'secondary'
  }
}

const refreshedText = computed(() =>
  refreshedAt.value ? t('voiceJobsRefreshed', { time: refreshedAt.value.toLocaleTimeString(locale.value) }) : '',
)

/** "26–50 of 137", so it is clear that the page is a section and how large the rest is. */
const rangeText = computed(() =>
  total.value === 0
    ? ''
    : t('voiceJobsRange', {
        from: offset.value + 1,
        to: Math.min(offset.value + jobs.value.length, total.value),
        total: total.value,
      }),
)

const hasPrevious = computed(() => offset.value > 0)
const hasNext = computed(() => offset.value + jobs.value.length < total.value)

onUnmounted(() => stop())

async function open(): Promise<void> {
  error.value = null
  offset.value = 0
  visible.value = true
  await refresh()
  schedule()
}

/** A state was picked: the paging starts over, since the page a job sat on says nothing about the next filter. */
async function narrow(): Promise<void> {
  offset.value = 0
  await refresh()
  schedule()
}

/** One page forward or back; the service is asked again, since it has moved on meanwhile. */
async function turn(by: number): Promise<void> {
  offset.value = Math.max(0, offset.value + by * pageSize)
  await refresh()
  schedule()
}

function close(): void {
  stop()
  visible.value = false
}

function schedule(): void {
  window.clearTimeout(timer)
  if (unsupported.value) {
    return
  }

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
    const result = await listVoiceJobs(pageSize, offset.value, filter.value, controller.signal)
    jobs.value = result.jobs
    total.value = result.total
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

/**
 * What can still be done with a job: a waiting or running one is cancelled, a finished one whose result is
 * still there is removed. A job the service has already cleared away stays in the list as a record, and there
 * is nothing left to delete - saying so beats a button that answers 204 and changes nothing visible.
 */
function action(job: VoiceJob): 'cancel' | 'delete' | null {
  if (status(job) === 'QUEUED' || status(job) === 'RUNNING') {
    return 'cancel'
  }
  return job.hasResult ? 'delete' : null
}

/**
 * A finished job still holding its result can be fetched here too. The panel only knows this session's job,
 * and one started in another browser or tab has no panel at all - without this, a result could only be
 * reached by starting the conversion over. Fetching changes nothing at the service: the result stays until it
 * is deleted or the retention is over, so the row keeps both buttons afterwards.
 */
function canDownload(job: VoiceJob): boolean {
  return status(job) === 'COMPLETED' && job.hasResult
}

/** Fetches a finished job's converted vocals as a WAV. */
async function fetchResult(job: VoiceJob): Promise<void> {
  if (fetching.value) {
    return
  }

  fetching.value = job.id
  error.value = null
  try {
    download(await downloadVoiceResult(job.id), `voice-${shortId(job.id)}.wav`)
  } catch (caught) {
    fail(caught)
    // The result was cleared away between the last refresh and the click; the list is what says so.
    if (caught instanceof ApiError && (caught.status === 404 || caught.status === 410)) {
      await refresh()
    }
  } finally {
    fetching.value = null
  }
}

/** Cancels a waiting job or removes a finished one. */
async function remove(job: VoiceJob): Promise<void> {
  const key = action(job) === 'cancel' ? 'voiceJobsCancelConfirm' : 'voiceJobsDeleteConfirm'
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

  // The only job of a page is gone: the page before it is now the last one, so it is shown instead of nothing.
  if (jobs.value.length === 1 && offset.value > 0) {
    offset.value -= pageSize
  }
  await refresh()
}

function status(job: VoiceJob): string {
  return job.status.toUpperCase()
}

/** What a state is called, for a job's row and for the filter alike. */
function statusText(state: string): string {
  switch (state.toUpperCase()) {
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
      return state
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
      : caught instanceof ApiError && (caught.status === 429 || caught.status === 503)
        ? // A rate limit or a full queue: the same request is worth repeating later, unlike a refused recording.
          t('voiceRetryLater', { message: caught.message })
        : t('voiceJobsError', { message: caught instanceof Error ? caught.message : String(caught) })
}

defineExpose({ open })
</script>

<template>
  <Dialog v-model:visible="visible" modal :style="{ width: 'min(44rem, calc(100vw - 2rem))' }" @hide="stop">
    <template #header>
      <div class="flex flex-1 flex-wrap items-center justify-between gap-x-4 gap-y-2 pr-2">
        <span class="text-lg font-semibold">{{ t('voiceJobsTitle') }}</span>
        <div v-if="!unsupported" class="flex flex-wrap items-center gap-2 text-xs">
          <Select
            v-model="filter"
            size="small"
            :options="filterOptions"
            option-label="label"
            option-value="value"
            :aria-label="t('voiceJobsFilter')"
            @change="narrow"
          />
          <span class="muted">{{ loading ? t('voiceJobsLoading') : refreshedText }}</span>
          <Button
            size="small"
            severity="secondary"
            outlined
            :label="t('voiceJobsRefresh')"
            :disabled="loading"
            @click="refresh"
          />
        </div>
      </div>
    </template>

    <p class="muted mt-0 mb-4 text-sm">{{ t('voiceJobsIntro') }}</p>

    <p v-if="error" class="hint danger mt-0 mb-3" role="alert">{{ error }}</p>
    <p v-if="unsupported" class="hint muted mt-0 mb-3">{{ t('voiceJobsUnsupported') }}</p>

    <table v-if="jobs.length > 0" class="mb-4 w-full border-collapse text-sm">
      <thead>
        <!-- Narrow screens do without the voice and the two timestamps; the status and the button matter there. -->
        <tr class="text-left text-[0.8125rem] text-muted-color">
          <th class="py-1.5 pr-2 font-medium">{{ t('voiceJobsJob') }}</th>
          <th class="py-1.5 pr-2 font-medium max-sm:hidden">{{ t('voiceJobsVoice') }}</th>
          <th class="py-1.5 pr-2 font-medium">{{ t('voiceJobsStatus') }}</th>
          <th class="py-1.5 pr-2 font-medium max-sm:hidden">{{ t('voiceJobsCreated') }}</th>
          <th class="py-1.5 pr-2 font-medium max-sm:hidden">{{ t('voiceJobsFinished') }}</th>
          <th class="py-1.5">
            <span class="sr-only">{{ t('voiceJobsActions') }}</span>
          </th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="job in jobs"
          :key="job.id"
          class="border-t border-surface align-top"
          :class="{ 'bg-highlight': job.id === props.ownJob }"
        >
          <td class="py-1.5 pr-2">
            <code class="font-mono text-[0.85rem]" :title="job.id">{{ shortId(job.id) }}</code>
            <span v-if="job.id === props.ownJob" class="block text-xs text-muted-color">{{ t('voiceJobsOwn') }}</span>
            <span v-if="job.errorMessage" class="block max-w-96 text-xs text-(--danger)">
              {{ job.errorMessage }}
            </span>
          </td>
          <td class="py-1.5 pr-2 text-[0.85rem] max-sm:hidden">{{ job.voiceLabel ?? job.voiceId ?? '–' }}</td>
          <td class="py-1.5 pr-2">
            <Tag :severity="severity(job)" :value="statusText(job.status)" class="whitespace-nowrap" />
          </td>
          <td class="py-1.5 pr-2 whitespace-nowrap max-sm:hidden">{{ when(job.createdUtc) }}</td>
          <td class="py-1.5 pr-2 whitespace-nowrap max-sm:hidden">{{ when(job.finishedUtc) }}</td>
          <td class="py-0.5 text-right whitespace-nowrap">
            <Button
              v-if="canDownload(job)"
              link
              size="small"
              :label="fetching === job.id ? t('voiceJobsDownloading') : t('voiceJobsDownload')"
              :disabled="fetching !== null || removing !== null"
              @click="fetchResult(job)"
            />
            <Button
              v-if="action(job)"
              link
              size="small"
              severity="danger"
              :label="action(job) === 'cancel' ? t('voiceJobsCancel') : t('voiceJobsDelete')"
              :disabled="removing !== null || fetching !== null"
              @click="remove(job)"
            />
            <span v-else-if="!canDownload(job)" class="muted text-xs">{{ t('voiceJobsCleared') }}</span>
          </td>
        </tr>
      </tbody>
    </table>
    <p v-else-if="!error && !unsupported" class="muted mt-0 mb-4 text-sm">
      {{ loading && !refreshedAt ? t('voiceJobsLoading') : t('voiceJobsEmpty') }}
    </p>

    <template #footer>
      <div class="flex w-full flex-wrap items-center justify-between gap-x-4 gap-y-2">
        <div v-if="!unsupported && total > 0" class="flex flex-wrap items-center gap-2 text-xs">
          <span class="muted">{{ rangeText }}</span>
          <Button
            size="small"
            severity="secondary"
            outlined
            icon="pi pi-chevron-left"
            :label="t('voiceJobsPrevious')"
            :disabled="!hasPrevious || loading"
            @click="turn(-1)"
          />
          <Button
            size="small"
            severity="secondary"
            outlined
            icon="pi pi-chevron-right"
            icon-pos="right"
            :label="t('voiceJobsNext')"
            :disabled="!hasNext || loading"
            @click="turn(1)"
          />
        </div>
        <span v-else />
        <Button severity="secondary" outlined :label="t('voiceJobsClose')" @click="close" />
      </div>
    </template>
  </Dialog>
</template>
