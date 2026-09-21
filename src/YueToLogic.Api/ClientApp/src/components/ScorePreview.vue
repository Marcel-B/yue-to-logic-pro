<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, shallowRef, watch } from 'vue'
import { t } from '../i18n'
import { loadRoutings, saveRoutings } from '../options'
import {
  contentHeight,
  draw,
  GUTTER_WIDTH,
  lanesOf,
  ticksAtX,
  totalWidth,
  trackColour,
  xAtTicks,
} from '../pianoRoll'
import {
  AUDIO_OUTPUT,
  createPlayer,
  defaultRoutings,
  listMidiPorts,
  midiAlreadyAllowed,
  midiSupported,
  OutputPool,
  scheduleOf,
  testTone,
  type MidiPort,
  type Player,
  type Routing,
} from '../player'
import { playableVoices } from '../score'
import type { ScoreDocument } from '../types'

const props = defineProps<{ score: ScoreDocument; includeChords: boolean; stale: boolean }>()

const canvas = ref<HTMLCanvasElement | null>(null)
const viewport = ref<HTMLDivElement | null>(null)
const large = ref(false)
const pxPerBar = ref(32)
const viewportWidth = ref(0)
const playhead = ref<number | null>(null)
const playing = ref(false)
const ports = ref<MidiPort[]>([])
const note = ref<string | null>(null)
const canAskForMidi = ref(false)

// Kept out of Vue's deep reactivity: thousands of notes that nothing renders from directly.
const voices = shallowRef(playableVoices(props.score, props.includeChords))
const lanes = shallowRef(lanesOf(voices.value))

const trackIds = computed(() => voices.value.map((voice) => voice.id))
const routings = ref<Routing[]>(loadRoutings(trackIds.value, defaultRoutings(voices.value)))

const pool = new OutputPool(null)
const channels = Array.from({ length: 16 }, (_, index) => index)
let player: Player | null = null
let frame = 0
let scrollTicks = 0

const secondsPerTick = computed(() => 60 / props.score.tempoBpm / props.score.ticksPerQuarterNote)
const contentWidth = computed(() => totalWidth(props.score, pxPerBar.value) + GUTTER_WIDTH)
const height = computed(() => contentHeight(lanes.value))

function render(): void {
  if (canvas.value) {
    draw(canvas.value, {
      score: props.score,
      lanes: lanes.value,
      pxPerBar: pxPerBar.value,
      scrollTicks,
      playhead: playhead.value,
    })
  }
}

function onScroll(): void {
  if (viewport.value) {
    scrollTicks = ticksAtX(props.score, viewport.value.scrollLeft, pxPerBar.value)
    render()
  }
}

/** Keeps the playhead in view while playing, without fighting a scroll the user just made. */
function follow(ticks: number): void {
  const element = viewport.value
  if (!element) {
    return
  }
  const x = xAtTicks(props.score, ticks, pxPerBar.value)
  const visible = element.clientWidth - GUTTER_WIDTH
  if (x < element.scrollLeft || x > element.scrollLeft + visible - 40) {
    element.scrollLeft = Math.max(0, x - visible * 0.2)
  }
}

function step(): void {
  const seconds = player?.position() ?? null
  if (seconds === null) {
    playing.value = false
    render()
    return
  }
  playhead.value = seconds / secondsPerTick.value
  follow(playhead.value)
  render()
  frame = requestAnimationFrame(step)
}

function stop(): void {
  cancelAnimationFrame(frame)
  player?.stop()
  playing.value = false
  render()
}

/** Throws the player away, so the next play picks up changed routing or a new score. */
function release(): void {
  cancelAnimationFrame(frame)
  player?.stop()
  player = null
  playing.value = false
}

function play(fromTicks = playhead.value ?? 0): void {
  if (!player) {
    player = createPlayer(scheduleOf(props.score, voices.value, routings.value), pool, () => {
      playing.value = false
      playhead.value = null
      render()
    })
  }
  player.play(fromTicks * secondsPerTick.value)
  playing.value = true
  cancelAnimationFrame(frame)
  frame = requestAnimationFrame(step)
}

function toggle(): void {
  if (playing.value) {
    stop()
  } else {
    play()
  }
}

/** Back to bar 1, scrolled home; playback carries on from there if it was running. */
function rewind(): void {
  playhead.value = 0
  viewport.value?.scrollTo({ left: 0 })
  if (playing.value) {
    play(0)
  } else {
    render()
  }
}

/** Lifts every key on every output; the way out when an instrument hangs on a note. */
function panic(): void {
  stop()
  pool.silence()
}

/** A click on the roll moves the playhead there, and keeps playing if it was. */
function seek(event: MouseEvent): void {
  const element = viewport.value
  if (!element) {
    return
  }
  const bounds = element.getBoundingClientRect()
  const x = event.clientX - bounds.left - GUTTER_WIDTH + element.scrollLeft
  // Below the lanes is the horizontal scrollbar; dragging it should not move the playhead.
  if (x < 0 || event.clientY - bounds.top > height.value) {
    return
  }
  playhead.value = Math.min(props.score.lengthTicks, ticksAtX(props.score, x, pxPerBar.value))
  if (playing.value) {
    play(playhead.value)
  } else {
    render()
  }
}

/** Sends every track to the same output, the usual first step with a single interface. */
function routeAll(output: string): void {
  routings.value = routings.value.map((routing) => ({ ...routing, output }))
}

/** The canvas only covers what is on screen, so its width follows the viewport rather than the song. */
const observer = new ResizeObserver(() => {
  viewportWidth.value = viewport.value?.clientWidth ?? 0
  requestAnimationFrame(render)
})

async function loadPorts(): Promise<void> {
  const found = await listMidiPorts()
  pool.setAccess(found.access)
  ports.value = found.ports
  canAskForMidi.value = found.access === null && found.reason !== 'unsupported'
  note.value =
    found.reason === 'unsupported' ? t('midiUnsupported') : found.reason === 'denied' ? t('midiDenied') : null

  if (found.access) {
    // Ports come and go while the page is open.
    found.access.onstatechange = async () => {
      ports.value = (await listMidiPorts()).ports
      const gone = routings.value.some(
        (routing) => routing.output !== AUDIO_OUTPUT && !ports.value.some((port) => port.id === routing.output),
      )
      if (gone) {
        routings.value = routings.value.map((routing) =>
          routing.output !== AUDIO_OUTPUT && !ports.value.some((port) => port.id === routing.output)
            ? { ...routing, output: AUDIO_OUTPUT }
            : routing,
        )
      }
    }
  }
}

onMounted(async () => {
  if (viewport.value) {
    observer.observe(viewport.value)
    viewportWidth.value = viewport.value.clientWidth
  }
  // The canvas takes its width from a style Vue has not applied yet on this tick.
  requestAnimationFrame(render)

  if (!midiSupported()) {
    note.value = t('midiUnsupported')
  } else if (await midiAlreadyAllowed()) {
    await loadPorts()
  } else {
    canAskForMidi.value = true
  }
})

onBeforeUnmount(() => {
  observer.disconnect()
  release()
  pool.close()
  window.removeEventListener('pagehide', stop)
})

// Hardware would keep sounding if the page went away mid-note.
window.addEventListener('pagehide', stop)

watch(
  () => [props.score, props.includeChords] as const,
  ([score, includeChords]) => {
    release()
    playhead.value = null
    voices.value = playableVoices(score, includeChords)
    lanes.value = lanesOf(voices.value)
    routings.value = loadRoutings(trackIds.value, defaultRoutings(voices.value))
    scrollTicks = 0
    if (viewport.value) {
      viewport.value.scrollLeft = 0
    }
    render()
  },
)

// Re-routing rebuilds the schedule; playback picks up where it was rather than jumping back to the start.
watch(
  routings,
  (value) => {
    saveRoutings(trackIds.value, value)
    const resume = playing.value ? (playhead.value ?? 0) : null
    release()
    if (resume !== null) {
      play(resume)
    }
  },
  { deep: true },
)

// Zooming keeps the bar at the left edge in place; the raw scroll offset would otherwise jump to a
// different part of the song every time the scale changes.
watch(pxPerBar, (value) => {
  const anchor = scrollTicks
  requestAnimationFrame(() => {
    if (viewport.value) {
      viewport.value.scrollLeft = Math.max(0, xAtTicks(props.score, anchor, value))
    }
    onScroll()
  })
})

watch([large, viewportWidth], () => requestAnimationFrame(onScroll))
</script>

<template>
  <section class="card preview" :class="{ large }">
    <div class="head">
      <h2>{{ t('previewTitle') }}</h2>
      <div class="controls">
        <button type="button" class="button primary small" :aria-pressed="playing" @click="toggle">
          {{ playing ? t('previewStop') : t('previewPlay') }}
        </button>
        <button
          type="button"
          class="button secondary small"
          :title="t('previewRewindTitle')"
          :disabled="!playing && !playhead"
          @click="rewind"
        >
          {{ t('previewRewind') }}
        </button>
        <button type="button" class="button secondary small" :title="t('previewPanicTitle')" @click="panic">
          {{ t('previewPanic') }}
        </button>
        <label class="field zoom" :title="t('previewZoom')">
          <span class="sr-only">{{ t('previewZoom') }}</span>
          <input v-model.number="pxPerBar" type="range" min="6" max="120" step="2" />
        </label>
        <button type="button" class="button secondary small" :aria-pressed="large" @click="large = !large">
          {{ large ? t('previewSmaller') : t('previewLarger') }}
        </button>
      </div>
    </div>

    <p v-if="stale" class="hint warning">{{ t('stale') }}</p>

    <div ref="viewport" class="viewport" @scroll.passive="onScroll" @click="seek">
      <div class="content" :style="{ width: `${contentWidth}px`, height: `${height}px` }">
        <canvas ref="canvas" :style="{ width: `${viewportWidth}px`, height: `${height}px` }" />
      </div>
    </div>

    <details class="routing" open>
      <summary>{{ t('previewRouting') }}</summary>

      <div class="bulk">
        <button v-if="canAskForMidi" type="button" class="button secondary small" @click="loadPorts">
          {{ t('previewFindMidi') }}
        </button>
        <label class="field">
          {{ t('previewRouteAll') }}
          <select @change="routeAll(($event.target as HTMLSelectElement).value)">
            <option value="">{{ t('previewRouteAllPick') }}</option>
            <option :value="AUDIO_OUTPUT">{{ t('previewOutputAudio') }}</option>
            <option v-for="port in ports" :key="port.id" :value="port.id">{{ port.name }}</option>
          </select>
        </label>
      </div>

      <table>
        <thead>
          <tr>
            <th>{{ t('previewTrack') }}</th>
            <th>{{ t('previewOutput') }}</th>
            <th>{{ t('previewChannel') }}</th>
            <th><span class="sr-only">{{ t('previewTest') }}</span></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(routing, index) in routings" :key="trackIds[index]" :class="{ muted: routing.muted }">
            <td>
              <label class="track">
                <input v-model="routing.muted" type="checkbox" :true-value="false" :false-value="true" />
                <span class="swatch" :style="{ background: trackColour(index) }" />
                {{ trackIds[index] }}
              </label>
            </td>
            <td>
              <select v-model="routing.output">
                <option :value="AUDIO_OUTPUT">{{ t('previewOutputAudio') }}</option>
                <option v-for="port in ports" :key="port.id" :value="port.id">{{ port.name }}</option>
              </select>
            </td>
            <td>
              <select v-model.number="routing.channel" :disabled="routing.output === AUDIO_OUTPUT">
                <option v-for="channel in channels" :key="channel" :value="channel">{{ channel + 1 }}</option>
              </select>
            </td>
            <td>
              <button
                type="button"
                class="button secondary small"
                @click="testTone(pool, routing, voices[index]?.kind === 'Drums')"
              >
                {{ t('previewTest') }}
              </button>
            </td>
          </tr>
        </tbody>
      </table>
    </details>

    <p class="muted hint">{{ note ?? t('previewHint') }}</p>
  </section>
</template>

<style scoped>
.head {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.head h2 {
  margin: 0;
}

.controls {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.5rem;
}

.zoom input {
  width: 8rem;
}

/*
 * A grid item will not shrink below its content by default, and the content here is the whole song - at 204
 * bars well over 6000 pixels, which would drag the entire page wide. This keeps the card inside the column.
 */
.preview {
  min-width: 0;
}

/*
 * The content element carries the scrollable width; the canvas sticks to the left edge of the scrollport and
 * is only as wide as what is visible, so a long song costs no extra pixels.
 */
.viewport {
  max-width: 100%;
  overflow-x: auto;
  overflow-y: hidden;
  margin-top: 0.75rem;
  border: 1px solid var(--border);
  border-radius: var(--radius-small);
  cursor: crosshair;
}

.content canvas {
  position: sticky;
  left: 0;
  display: block;
}

.routing {
  margin-top: 0.75rem;
}

.routing summary {
  cursor: pointer;
  font-weight: 600;
}

.bulk {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.75rem;
  margin: 0.75rem 0 0.25rem;
}

.bulk .field {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: var(--text-muted);
  font-size: 0.875rem;
}

.routing table {
  width: 100%;
  border-collapse: collapse;
}

.routing th {
  padding: 0.35rem 0.5rem 0.35rem 0;
  color: var(--text-muted);
  font-size: 0.8125rem;
  font-weight: 500;
  text-align: left;
}

.routing td {
  padding: 0.2rem 0.5rem 0.2rem 0;
  vertical-align: middle;
}

.routing tr.muted {
  opacity: 0.5;
}

.track {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  white-space: nowrap;
}

.swatch {
  width: 0.75rem;
  height: 0.75rem;
  border-radius: 3px;
}

.routing select {
  width: 100%;
  min-width: 0;
}

.routing td:nth-child(3) select {
  width: 4.5rem;
}

.preview.large {
  position: fixed;
  inset: 1rem;
  z-index: 20;
  overflow: auto;
  box-shadow: 0 1.5rem 3rem rgb(0 0 0 / 0.25);
}
</style>
