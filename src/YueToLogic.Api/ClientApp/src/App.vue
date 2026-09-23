<script setup lang="ts">
import { ref, useTemplateRef, watch } from 'vue'
import {
  ApiError,
  assignInstrument,
  convertScore,
  exportLogicProject,
  listAssignments,
  listInstruments,
  listVoices,
  LogicExportError,
  stemsAvailable,
  voiceAvailable,
} from './api'
import InstrumentDialog from './components/InstrumentDialog.vue'
import OptionsForm from './components/OptionsForm.vue'
import ResultView from './components/ResultView.vue'
import ScorePreview from './components/ScorePreview.vue'
import FileDropZone from './components/FileDropZone.vue'
import StemPanel from './components/StemPanel.vue'
import StemServiceDialog from './components/StemServiceDialog.vue'
import VoiceDialog from './components/VoiceDialog.vue'
import VoiceJobsDialog from './components/VoiceJobsDialog.vue'
import VoicePanel from './components/VoicePanel.vue'
import type { SongFolder } from './folder'
import { locale, setLocale, t } from './i18n'
import { instrumentsForExport, withDrumNotes, withInstrumentChannels } from './instruments'
import { deletePreset, loadPresets, savePreset, type Preset } from './presets'
import { audioSeconds, clearFormState, defaultFormState, loadFormState, saveFormState, toConversionOptions } from './options'
import { baseName, download } from './score'
import { STEMS_NOT_TAKEN, VOICE_NOT_TAKEN, type Assignments, type ConversionResult, type Diagnostic, type Instrument, type ReferenceVoice } from './types'

const file = ref<File | null>(null)
const audio = ref<File | null>(null)
/** Length of the chosen audio.flac, read from its header; the tempo fit is measured against it. */
const audioLength = ref<number | null>(null)
/** What came out of a dropped folder: nothing usable, or which of several songs was taken. */
const folderNote = ref<string | null>(null)

/** Whether this server can have stems separated; without a stem service the panel stays away. */
const stems = ref(false)
/** A finished separation, ready to go into the next Logic project. */
const stemJob = ref<string | null>(null)
/** A separation in progress; the result view says so next to its Logic button. */
const stemsRunning = ref(false)
/** The job the stem panel is about, running or finished; the service dialog marks it as this session's. */
const stemTracked = ref<string | null>(null)
const stemPanel = useTemplateRef<InstanceType<typeof StemPanel>>('stemPanel')
const stemServiceDialog = useTemplateRef<InstanceType<typeof StemServiceDialog>>('stemServiceDialog')
void stemsAvailable().then((available) => (stems.value = available))

function openStemService(): void {
  stemServiceDialog.value?.open()
}

/** A job removed in the service dialog: if it was this session's, the panel and the export let go of it too. */
function stemJobDeleted(id: string): void {
  stemPanel.value?.forget(id)
  if (stemJob.value === id) {
    stemJob.value = null
  }
}

/** Whether this server can have a voice changed; without a voice service the section stays away. */
const voice = ref(false)
/** The collection of model voices, kept by the voice service; the panel chooses from it, the dialog edits it. */
const voices = ref<ReferenceVoice[]>([])
/** A finished conversion, whose vocals go onto the next Logic project's vocals track. */
const voiceJob = ref<string | null>(null)
/** A conversion in progress; the result view says so next to its Logic button. */
const voiceRunning = ref(false)
/** The job the voice panel is about, running or finished; the job dialog marks it as this session's. */
const voiceTracked = ref<string | null>(null)
const voicePanel = useTemplateRef<InstanceType<typeof VoicePanel>>('voicePanel')
const voiceDialog = useTemplateRef<InstanceType<typeof VoiceDialog>>('voiceDialog')
const voiceJobsDialog = useTemplateRef<InstanceType<typeof VoiceJobsDialog>>('voiceJobsDialog')
void voiceAvailable().then(async (available) => {
  voice.value = available
  if (available) {
    // Without the collection the panel has nothing to choose from; a service that does not answer leaves it empty.
    voices.value = await listVoices().catch(() => [])
  }
})

function openVoices(): void {
  voiceDialog.value?.open()
}

function openVoiceJobs(): void {
  voiceJobsDialog.value?.open()
}

/** A job removed in the job dialog: if it was this session's, the panel and the export let go of it too. */
function voiceJobDeleted(id: string): void {
  voicePanel.value?.forget(id)
  if (voiceJob.value === id) {
    voiceJob.value = null
  }
}

/** The instrument library and which track plays which, both kept on the server. */
const instruments = ref<Instrument[]>([])
/**
 * Which track plays which instrument. This is configuration, not playback: it decides the channel a track is
 * written on and the hardware the Logic project addresses, both of which the server produces. It therefore
 * applies in every browser - only sending the preview to a MIDI port needs Web MIDI, which Chromium alone has.
 */
const assignments = ref<Assignments>({})
const instrumentsError = ref<string | null>(null)
const instrumentDialog = useTemplateRef<InstanceType<typeof InstrumentDialog>>('instrumentDialog')
void loadInstruments()

async function loadInstruments(): Promise<void> {
  try {
    ;[instruments.value, assignments.value] = await Promise.all([listInstruments(), listAssignments()])
  } catch (caught) {
    // Without the library everything else still works; the routing table then shows ports and channels only.
    instrumentsError.value = t('instrumentsError', { message: caught instanceof Error ? caught.message : String(caught) })
  }
}

function openInstruments(): void {
  instrumentDialog.value?.open()
}

/** The list after the dialog changed it; a track whose instrument is gone loses its assignment, as on the server. */
function instrumentsChanged(list: Instrument[]): void {
  instruments.value = list
  const ids = new Set(list.map((instrument) => instrument.id))
  assignments.value = Object.fromEntries(Object.entries(assignments.value).filter(([, id]) => ids.has(id)))
}

/** Shown at once and sent to the server; if that fails the previous choice comes back. */
async function assign(track: string, instrumentId: number | null): Promise<void> {
  const before = { ...assignments.value }
  const next = { ...assignments.value }
  if (instrumentId === null) {
    delete next[track]
  } else {
    next[track] = instrumentId
  }
  assignments.value = next
  instrumentsError.value = null
  try {
    await assignInstrument(track, instrumentId)
  } catch (caught) {
    assignments.value = before
    instrumentsError.value =
      caught instanceof ApiError && caught.status === 0
        ? t('networkError')
        : t('instrumentsError', { message: caught instanceof Error ? caught.message : String(caught) })
  }
}

/** The channel a track plays on depends on its instrument, so the downloads are only current for the assignment they were made with. */
watch(assignments, () => {
  if (result.value) {
    stale.value = true
  }
})

/** The presets, kept on the server like the instruments; loaded once, and again after every change. */
const presets = ref<Preset[]>([])
/** The preset the form currently shows; empty once a preset is saved under a new name or none is chosen. */
const presetName = ref('')
const newPresetName = ref('')
const presetsBusy = ref(false)
const presetsError = ref<string | null>(null)
void withPresets(() => loadPresets())

/** Runs a change of the presets and shows what came back; the form itself is unaffected by a failure. */
async function withPresets(change: () => Promise<Preset[]>): Promise<boolean> {
  presetsBusy.value = true
  presetsError.value = null
  try {
    presets.value = await change()
    return true
  } catch (caught) {
    presetsError.value =
      caught instanceof ApiError && caught.status === 0
        ? t('networkError')
        : t('presetsError', { message: caught instanceof Error ? caught.message : String(caught) })
    return false
  } finally {
    presetsBusy.value = false
  }
}

function applyPreset(): void {
  const preset = presets.value.find((entry) => entry.name === presetName.value)
  if (preset) {
    form.value = { ...preset.form }
  }
}

async function storePreset(): Promise<void> {
  const name = newPresetName.value
  if (await withPresets(() => savePreset(name, form.value))) {
    // The server keeps the name as saved; pick it as it now stands so the select shows it.
    presetName.value = presets.value.find((entry) => entry.name.toLowerCase() === name.toLowerCase())?.name ?? ''
    newPresetName.value = ''
  }
}

async function removePreset(): Promise<void> {
  const name = presetName.value
  if (await withPresets(() => deletePreset(name))) {
    presetName.value = ''
  }
}
const logicBusy = ref(false)
const logicError = ref<string | null>(null)
const logicWarnings = ref<Diagnostic[]>([])
const form = ref(loadFormState())
const outputName = ref('score')
const result = ref<ConversionResult | null>(null)
const stale = ref(false)
const busy = ref(false)
const error = ref<string | null>(null)

let pending: AbortController | null = null

watch(
  form,
  (value) => {
    saveFormState(value)
    if (result.value) {
      stale.value = true
    }
  },
  { deep: true },
)

function selectFile(selected: File): void {
  file.value = selected
  outputName.value = baseName(selected.name)
  result.value = null
  stale.value = false
  error.value = null
  logicError.value = null
  logicWarnings.value = []
}

async function selectAudio(selected: File | null): Promise<void> {
  audio.value = selected
  logicError.value = null
  logicWarnings.value = []
  audioLength.value = selected ? await audioSeconds(selected) : null
  if (audioLength.value === null) {
    form.value.fitTempo = false
  }
}

/** A dropped YuE folder fills both files at once; with several songs in it the first one is taken. */
async function selectSongs(songs: SongFolder[]): Promise<void> {
  folderNote.value = null
  if (songs.length === 0) {
    folderNote.value = t('folderNoScore')
    return
  }

  const song = songs[0]!
  selectFile(song.score)
  await selectAudio(song.audio)
  if (songs.length > 1) {
    folderNote.value = t('folderSongs').replace('{0}', String(songs.length)).replace('{1}', song.name)
  }
}

async function exportLogic(): Promise<void> {
  if (!file.value) {
    return
  }

  logicBusy.value = true
  logicError.value = null
  logicWarnings.value = []
  try {
    const exported = await exportLogicProject(
      file.value,
      audio.value,
      conversionOptions(),
      outputName.value,
      form.value.splitSections,
      stemJob.value,
      voiceJob.value,
      instrumentsForExport(assignments.value, instruments.value),
    )
    logicWarnings.value = exported.warnings
    download(exported.zip, exported.fileName)
    // Only what went into the project was confirmed and is now gone from its service. What the server could
    // not take - vocals of another sample rate, say - stays there, and so does the job here: otherwise the
    // panel would drop the only way of downloading the result.
    const notTaken = (code: string) => exported.warnings.some((warning) => warning.code === code)
    if (!notTaken(STEMS_NOT_TAKEN)) {
      stemJob.value = null
    }
    if (notTaken(VOICE_NOT_TAKEN)) {
      voicePanel.value?.notTaken()
    } else {
      voiceJob.value = null
    }
  } catch (caught) {
    logicError.value =
      caught instanceof LogicExportError
        ? `${t('logicFailed')}: ${caught.message}`
        : caught instanceof ApiError && caught.status === 0
          ? t('networkError')
          : `${t('logicFailed')}: ${caught instanceof Error ? caught.message : String(caught)}`
  } finally {
    logicBusy.value = false
  }
}

/** Back to a fresh start: no files, default parameters, no result. The language is kept. */
function reset(): void {
  pending?.abort()
  pending = null
  busy.value = false
  file.value = null
  audio.value = null
  folderNote.value = null
  stemJob.value = null
  voiceJob.value = null
  audioLength.value = null
  form.value = defaultFormState()
  presetName.value = ''
  newPresetName.value = ''
  clearFormState()
  outputName.value = 'score'
  result.value = null
  stale.value = false
  error.value = null
  logicBusy.value = false
  logicError.value = null
  logicWarnings.value = []
}

/** The form's options with every assigned track on its instrument's channel, and the drums on a drum machine's notes. */
function conversionOptions() {
  const options = withInstrumentChannels(toConversionOptions(form.value, audioLength.value), assignments.value, instruments.value)
  return withDrumNotes(options, assignments.value, instruments.value)
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

  try {
    result.value = await convertScore(file.value, conversionOptions(), controller.signal)
    stale.value = false
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
</script>

<template>
  <header class="page-header">
    <div>
      <h1>YuE <span aria-hidden="true">→</span> Logic</h1>
      <p class="muted">{{ t('subtitle') }}</p>
    </div>
    <div class="header-actions">
      <div class="tools" role="group">
        <!-- Icons rather than words: the header stays one line, and the title says what each one opens. -->
        <button type="button" class="icon-button" :title="t('instrumentsManageTitle')" :aria-label="t('instrumentsManage')" @click="openInstruments">
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <line x1="4" y1="6" x2="20" y2="6" />
            <line x1="4" y1="12" x2="20" y2="12" />
            <line x1="4" y1="18" x2="20" y2="18" />
            <circle cx="9" cy="6" r="2" />
            <circle cx="15" cy="12" r="2" />
            <circle cx="7" cy="18" r="2" />
          </svg>
        </button>
        <button
          v-if="stems"
          type="button"
          class="icon-button"
          :title="t('stemServiceManageTitle')"
          :aria-label="t('stemServiceManage')"
          @click="openStemService"
        >
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <path d="M2 12h2l2-6 3 12 3-14 3 16 3-10 2 2h2" />
          </svg>
        </button>
        <button
          v-if="voice"
          type="button"
          class="icon-button"
          :title="t('voicesManageTitle')"
          :aria-label="t('voicesManage')"
          @click="openVoices"
        >
          <!-- A microphone: the collection of voices. -->
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <rect x="9" y="3" width="6" height="11" rx="3" />
            <path d="M5 11a7 7 0 0 0 14 0" />
            <line x1="12" y1="18" x2="12" y2="21" />
          </svg>
        </button>
        <button
          v-if="voice"
          type="button"
          class="icon-button"
          :title="t('voiceJobsManageTitle')"
          :aria-label="t('voiceJobsManage')"
          @click="openVoiceJobs"
        >
          <!-- A microphone in a list: the jobs of the voice service. -->
          <svg viewBox="0 0 24 24" aria-hidden="true">
            <line x1="4" y1="7" x2="13" y2="7" />
            <line x1="4" y1="12" x2="11" y2="12" />
            <line x1="4" y1="17" x2="13" y2="17" />
            <circle cx="18" cy="12" r="3" />
            <line x1="18" y1="15" x2="18" y2="19" />
          </svg>
        </button>
      </div>
      <button type="button" class="button secondary small" :title="t('resetTitle')" @click="reset">{{ t('reset') }}</button>
      <div class="locale" role="group" aria-label="Language">
      <button type="button" :aria-pressed="locale === 'de'" @click="setLocale('de')">DE</button>
      <button type="button" :aria-pressed="locale === 'en'" @click="setLocale('en')">EN</button>
      </div>
    </div>
  </header>

  <main>
    <p v-if="instrumentsError" class="hint danger" role="alert">{{ instrumentsError }}</p>

    <div class="files">
      <section class="card">
        <h2>{{ t('scoreTitle') }}</h2>
        <FileDropZone
          :file="file"
          extension=".abc"
          accept=".abc,text/plain,text/vnd.abc"
          :drop-hint="t('dropHint')"
          :wrong-type-hint="t('notAbc')"
          folders
          @select="selectFile"
          @songs="selectSongs"
          @clear="file = null"
        />
        <p class="hint muted">{{ t('folderHint') }}</p>
        <p v-if="folderNote" class="hint">{{ folderNote }}</p>
      </section>

      <section class="card">
        <h2>{{ t('audioTitle') }}</h2>
        <p class="muted intro">{{ t('audioInfo') }}</p>
        <FileDropZone
          :file="audio"
          extension=".flac"
          accept=".flac,audio/flac,audio/x-flac"
          :drop-hint="t('audioDropHint')"
          :wrong-type-hint="t('notFlac')"
          @select="selectAudio"
          @clear="selectAudio(null)"
        />
        <StemPanel
          v-if="stems"
          ref="stemPanel"
          v-model:job="stemJob"
          v-model:running="stemsRunning"
          v-model:tracked="stemTracked"
          :audio="audio"
          :output-name="outputName"
          @export-logic="exportLogic"
        />
        <VoicePanel
          v-if="voice"
          ref="voicePanel"
          v-model:job="voiceJob"
          v-model:running="voiceRunning"
          v-model:tracked="voiceTracked"
          :voices="voices"
          :stem-job="stemJob"
          :audio="audio"
          :output-name="outputName"
          @export-logic="exportLogic"
        />
      </section>
    </div>

    <section class="card">
      <div class="options-head">
        <h2>{{ t('optionsTitle') }}</h2>
        <div class="presets">
          <label>
            <span class="sr-only">{{ t('presets') }}</span>
            <select v-model="presetName" @change="applyPreset">
              <option value="">{{ t('presetNone') }}</option>
              <option v-for="preset in presets" :key="preset.name" :value="preset.name">{{ preset.name }}</option>
            </select>
          </label>
          <input v-model.trim="newPresetName" type="text" :placeholder="t('presetName')" spellcheck="false" />
          <button type="button" class="button secondary small" :disabled="!newPresetName || presetsBusy" @click="storePreset">
            {{ t('presetSave') }}
          </button>
          <button type="button" class="button secondary small" :disabled="!presetName || presetsBusy" @click="removePreset">
            {{ t('presetDelete') }}
          </button>
        </div>
      </div>
      <p v-if="presetsError" class="hint danger presets-error" role="alert">{{ presetsError }}</p>
      <OptionsForm v-model="form" :has-audio="audioLength !== null" />

      <form class="submit" @submit.prevent="convert">
        <label>
          {{ t('outputName') }}
          <span class="name-field">
            <input v-model.trim="outputName" type="text" required spellcheck="false" />
            <span class="muted">.mid</span>
          </span>
        </label>
        <button type="submit" class="button primary" :disabled="!file || busy || !outputName">
          {{ busy ? t('converting') : t('convert') }}
        </button>
      </form>
      <p v-if="error" class="hint danger" role="alert">{{ error }}</p>
    </section>

    <ScorePreview
      v-if="result?.score"
      :score="result.score"
      :include-chords="form.includeChords"
      :stale="stale"
      :instruments="instruments"
      :assignments="assignments"
      @assign="assign"
      @manage-instruments="openInstruments"
    />

    <ResultView
      v-if="result"
      :result="result"
      :output-name="outputName"
      :stale="stale"
      :has-audio="audio !== null"
      :stems-running="stemsRunning"
      :stems-ready="stemJob !== null"
      :voice-running="voiceRunning"
      :voice-ready="voiceJob !== null"
      :logic-busy="logicBusy"
      :logic-error="logicError"
      :logic-warnings="logicWarnings"
      @export-logic="exportLogic"
    />

    <InstrumentDialog ref="instrumentDialog" :instruments="instruments" @changed="instrumentsChanged" />
    <StemServiceDialog v-if="stems" ref="stemServiceDialog" :own-job="stemTracked" @deleted="stemJobDeleted" />
    <VoiceDialog v-if="voice" ref="voiceDialog" :voices="voices" @changed="voices = $event" />
    <VoiceJobsDialog v-if="voice" ref="voiceJobsDialog" :own-job="voiceTracked" @deleted="voiceJobDeleted" />
  </main>
</template>

<style scoped>
.page-header {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1.5rem;
}

h1 {
  margin: 0;
  font-size: 1.75rem;
  letter-spacing: -0.02em;
}

.page-header p {
  margin: 0.25rem 0 0;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.button.small {
  padding: 0.3rem 0.75rem;
  font-size: 0.8rem;
}

.tools {
  display: flex;
  gap: 0.25rem;
}

.icon-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 2.1rem;
  height: 2.1rem;
  padding: 0;
  border: 1px solid var(--border-strong);
  border-radius: var(--radius-small);
  background: var(--surface);
  color: var(--text-muted);
  cursor: pointer;
  transition:
    color 0.15s,
    border-color 0.15s;
}

.icon-button:hover {
  border-color: var(--accent);
  color: var(--accent);
}

.icon-button svg {
  width: 1.15rem;
  height: 1.15rem;
  fill: none;
  stroke: currentcolor;
  stroke-width: 1.8;
  stroke-linecap: round;
  stroke-linejoin: round;
}

.locale {
  display: flex;
  overflow: hidden;
  border: 1px solid var(--border-strong);
  border-radius: var(--radius-small);
}

.locale button {
  padding: 0.3rem 0.65rem;
  border: 0;
  background: transparent;
  color: var(--text-muted);
  font: inherit;
  font-size: 0.8rem;
  cursor: pointer;
}

.locale button[aria-pressed='true'] {
  background: var(--accent);
  color: var(--on-accent);
}

main {
  display: grid;
  gap: 1rem;
}

.options-head {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  justify-content: space-between;
  gap: 0.5rem 1rem;
}

.options-head h2 {
  margin-bottom: 0;
}

.presets {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.35rem;
}

.presets input {
  width: 7rem;
}

.presets-error {
  margin: 0 0 0.75rem;
}

/* Score and audio side by side; below a certain width each one takes the whole row. */
.files {
  display: flex;
  flex-wrap: wrap;
  gap: 1rem;
}

.files > .card {
  display: flex;
  flex: 1 1 18rem;
  flex-direction: column;
}

/* The drop zone is a child component, so its own root element needs a deep selector. */
.files .card :deep(.drop-zone) {
  flex: 1;
}

.intro {
  margin: -0.5rem 0 1rem;
  font-size: 0.9rem;
}

.submit {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  justify-content: space-between;
  gap: 1rem;
  margin-top: 1.5rem;
  padding-top: 1.25rem;
  border-top: 1px solid var(--border);
}

.submit label {
  display: grid;
  flex: 1 1 14rem;
  gap: 0.25rem;
  color: var(--text-muted);
  font-size: 0.875rem;
}

.name-field {
  display: flex;
  align-items: center;
  gap: 0.35rem;
}

.name-field input {
  flex: 1;
  min-width: 0;
}
</style>
