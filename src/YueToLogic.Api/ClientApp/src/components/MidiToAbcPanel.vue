<script setup lang="ts">
import { computed, ref, useTemplateRef } from 'vue'
import { ApiError, convertMidi } from '../api'
import { formatNumber, t, type MessageKey } from '../i18n'
import { barAt, barCount, baseName, download } from '../score'
import type { Diagnostic, MidiToAbcResult, MidiTrackRole } from '../types'
import FileDropZone from './FileDropZone.vue'

/**
 * The way back: a MIDI file exported from Logic after editing the song there becomes a score.abc for YuE2 again.
 * The server does the reading; this panel shows which track became which voice, lets that be changed, and hands
 * the score over by the clipboard or as a file. It keeps nothing: every change asks the server again.
 */

const ROLES: MidiTrackRole[] = ['Vocal', 'Ins', 'Chords', 'Ignore']

const file = ref<File | null>(null)
const result = ref<MidiToAbcResult | null>(null)
/** Roles chosen by hand, by track index; the others keep what the server made of their names. */
const roles = ref<Record<number, MidiTrackRole>>({})
/** Empty for the automatic choice: the silent bars at the start. */
const skipBars = ref<number | ''>('')
const busy = ref(false)
const error = ref<string | null>(null)
const copied = ref(false)
const copyFailed = ref(false)
const showInfos = ref(false)
const text = useTemplateRef<HTMLTextAreaElement>('text')

let pending: AbortController | null = null
let copiedTimer: ReturnType<typeof setTimeout> | undefined

const score = computed(() => result.value?.score ?? null)
const warnings = computed(() => result.value?.diagnostics.filter((d) => d.severity !== 'Info') ?? [])
const infos = computed(() => result.value?.diagnostics.filter((d) => d.severity === 'Info') ?? [])

const facts = computed(() => {
  const doc = score.value
  if (!doc) {
    return ''
  }
  return t('midiFacts', {
    tempo: formatNumber(doc.tempoBpm, 2),
    meter: doc.timeSignatures.map((s) => `${s.numerator}/${s.denominator}`).join(', '),
    key: doc.keySignatures.map((k) => k.key).join(', '),
    bars: barCount(doc),
  })
})

const sections = computed(() => {
  const doc = score.value
  if (!doc || doc.sections.length === 0) {
    return ''
  }
  return t('midiSections', { sections: doc.sections.map((s) => `${s.name} (${barAt(doc, s.startTicks)})`).join(', ') })
})

function select(selected: File): void {
  file.value = selected
  roles.value = {}
  skipBars.value = ''
  void convert()
}

/** Back to nothing, as the page's reset button does it. */
function clear(): void {
  pending?.abort()
  pending = null
  busy.value = false
  file.value = null
  result.value = null
  roles.value = {}
  skipBars.value = ''
  error.value = null
  copied.value = false
  copyFailed.value = false
}

defineExpose({ clear })

function assign(index: number, role: MidiTrackRole): void {
  roles.value = { ...roles.value, [index]: role }
  void convert()
}

async function convert(): Promise<void> {
  if (!file.value) {
    return
  }

  pending?.abort()
  const controller = new AbortController()
  pending = controller
  busy.value = true
  error.value = null
  copied.value = false
  copyFailed.value = false
  try {
    result.value = await convertMidi(
      file.value,
      { trackRoles: roles.value, skipBars: skipBars.value === '' ? null : Math.max(0, Math.round(skipBars.value)) },
      controller.signal,
    )
  } catch (caught) {
    if (caught instanceof DOMException && caught.name === 'AbortError') {
      return
    }
    result.value = null
    error.value =
      caught instanceof ApiError && caught.status === 0
        ? t('networkError')
        : t('requestError', { message: caught instanceof Error ? caught.message : String(caught) })
  } finally {
    if (pending === controller) {
      busy.value = false
      pending = null
    }
  }
}

/** The clipboard API needs a secure context; over plain HTTP in the home network the old way still works. */
async function copy(): Promise<void> {
  const abc = result.value?.abc
  if (!abc) {
    return
  }

  copyFailed.value = false
  try {
    await navigator.clipboard.writeText(abc)
  } catch {
    text.value?.select()
    if (!document.execCommand('copy')) {
      copyFailed.value = true
      return
    }
  }

  copied.value = true
  clearTimeout(copiedTimer)
  copiedTimer = setTimeout(() => (copied.value = false), 2000)
}

function downloadAbc(): void {
  if (result.value?.abc && file.value) {
    download(new Blob([result.value.abc], { type: 'text/vnd.abc' }), `${baseName(file.value.name)}.abc`)
  }
}

function severityLabel(diagnostic: Diagnostic): string {
  return t(`severity_${diagnostic.severity}` as MessageKey)
}

function roleLabel(role: MidiTrackRole): string {
  return t(`midiRole_${role}` as MessageKey)
}
</script>

<template>
  <section class="card" aria-live="polite">
    <h2>{{ t('midiTitle') }}</h2>
    <p class="muted intro">{{ t('midiInfo') }}</p>
    <FileDropZone
      :file="file"
      :extension="['.mid', '.midi']"
      accept=".mid,.midi,audio/midi,audio/x-midi"
      :drop-hint="t('midiDropHint')"
      :wrong-type-hint="t('notMidi')"
      @select="select"
      @clear="clear"
    />

    <p v-if="busy" class="hint muted">{{ t('midiReading') }}</p>
    <p v-if="error" class="hint danger" role="alert">{{ error }}</p>

    <template v-if="result">
      <p v-if="!result.success" class="hint danger" role="alert">{{ t('midiFailed') }}</p>

      <template v-if="result.tracks.length">
        <h3>{{ t('midiTracks') }}</h3>
        <p class="hint muted">{{ t('midiTracksHint') }}</p>
        <ul class="tracks">
          <li v-for="track in result.tracks" :key="track.index">
            <span class="track-name">
              <span class="number">{{ track.index + 1 }}</span>
              {{ track.name }}
            </span>
            <span class="muted">{{ t('midiTrackChannel', { channel: track.channel }) }} · {{ t('notes', { count: track.noteCount }) }}</span>
            <label>
              <span class="sr-only">{{ track.name }}</span>
              <select :value="track.role" :disabled="busy" @change="assign(track.index, ($event.target as HTMLSelectElement).value as MidiTrackRole)">
                <option v-for="role in ROLES" :key="role" :value="role">{{ roleLabel(role) }}</option>
              </select>
            </label>
          </li>
        </ul>
      </template>

      <label class="skip">
        {{ t('midiSkipBars') }}
        <input
          v-model="skipBars"
          type="number"
          min="0"
          max="999"
          step="1"
          :placeholder="t('midiSkipBarsAuto')"
          :disabled="busy"
          @change="convert"
        />
      </label>
      <p class="hint muted">{{ t('midiSkipBarsHint') }}</p>

      <template v-if="result.abc">
        <h3>{{ t('midiAbc') }}</h3>
        <p class="facts">{{ facts }}</p>
        <p v-if="sections" class="facts muted">{{ sections }}</p>
        <textarea ref="text" class="abc" :value="result.abc" readonly spellcheck="false" rows="14" @focus="text?.select()" />
        <div class="actions">
          <button type="button" class="button primary" @click="copy">{{ copied ? t('midiCopied') : t('midiCopy') }}</button>
          <button type="button" class="button secondary" @click="downloadAbc">{{ t('midiDownload') }}</button>
        </div>
        <p v-if="copyFailed" class="hint warning" role="alert">{{ t('midiCopyFailed') }}</p>
      </template>

      <template v-if="result.diagnostics.length">
        <h3>{{ t('diagnostics') }}</h3>
        <ul v-if="warnings.length" class="diagnostics">
          <li v-for="(diagnostic, index) in warnings" :key="index" :class="diagnostic.severity.toLowerCase()">
            <span class="badge">{{ severityLabel(diagnostic) }}</span>
            <span class="code">{{ diagnostic.code }}</span>
            <span class="message">{{ diagnostic.message }}</span>
          </li>
        </ul>
        <template v-if="infos.length">
          <button v-if="!showInfos" type="button" class="link" @click="showInfos = true">
            {{ t('showInfos', { count: infos.length }) }}
          </button>
          <ul v-else class="diagnostics">
            <li v-for="(diagnostic, index) in infos" :key="index" class="info">
              <span class="badge">{{ severityLabel(diagnostic) }}</span>
              <span class="code">{{ diagnostic.code }}</span>
              <span class="message">{{ diagnostic.message }}</span>
            </li>
          </ul>
        </template>
      </template>
    </template>
  </section>
</template>

<style scoped>
.intro {
  margin: -0.5rem 0 1rem;
  font-size: 0.9rem;
}

h3 {
  margin: 1.25rem 0 0.5rem;
  font-size: 0.95rem;
}

.tracks {
  display: grid;
  gap: 0.25rem;
  margin: 0.5rem 0 0;
  padding: 0;
  list-style: none;
}

.tracks li {
  display: grid;
  grid-template-columns: minmax(8rem, 1fr) auto minmax(10rem, 16rem);
  align-items: center;
  gap: 0.5rem 1rem;
  padding: 0.35rem 0;
  border-bottom: 1px solid var(--border);
}

@media (max-width: 40rem) {
  .tracks li {
    grid-template-columns: 1fr;
  }
}

.track-name {
  overflow-wrap: anywhere;
}

.number {
  display: inline-block;
  min-width: 1.5rem;
  color: var(--text-muted);
  font-variant-numeric: tabular-nums;
}

.tracks select {
  width: 100%;
}

.skip {
  display: grid;
  gap: 0.25rem;
  max-width: 16rem;
  margin-top: 1.25rem;
  color: var(--text-muted);
  font-size: 0.875rem;
}

.facts {
  margin: 0 0 0.35rem;
  font-variant-numeric: tabular-nums;
}

.abc {
  box-sizing: border-box;
  width: 100%;
  margin-top: 0.5rem;
  padding: 0.75rem;
  border: 1px solid var(--border-strong);
  border-radius: var(--radius-small);
  background: var(--surface-sunken);
  color: var(--text);
  font-family: var(--font-mono);
  font-size: 0.8rem;
  line-height: 1.45;
  resize: vertical;
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 0.75rem;
  margin-top: 0.75rem;
}

.diagnostics {
  display: grid;
  gap: 0.5rem;
  margin: 0 0 0.5rem;
  padding: 0;
  list-style: none;
}

.diagnostics li {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 0.25rem 0.6rem;
  padding: 0.5rem 0.75rem;
  border-left: 3px solid var(--border-strong);
  border-radius: var(--radius-small);
  background: var(--surface-sunken);
  font-size: 0.9rem;
}

.diagnostics .error {
  border-left-color: var(--danger);
}

.diagnostics .warning {
  border-left-color: var(--warning);
}

.badge {
  font-weight: 600;
}

.error .badge {
  color: var(--danger);
}

.warning .badge {
  color: var(--warning-text);
}

.code {
  font-family: var(--font-mono);
  font-size: 0.8rem;
}

.message {
  flex-basis: 100%;
}
</style>
