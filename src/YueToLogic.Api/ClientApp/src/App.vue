<script setup lang="ts">
import { ref, watch } from 'vue'
import { ApiError, convertScore, exportLogicProject, LogicExportError } from './api'
import OptionsForm from './components/OptionsForm.vue'
import ResultView from './components/ResultView.vue'
import FileDropZone from './components/FileDropZone.vue'
import { locale, setLocale, t } from './i18n'
import { clearFormState, defaultFormState, loadFormState, saveFormState, toConversionOptions } from './options'
import { baseName, download } from './score'
import type { ConversionResult, Diagnostic } from './types'

const file = ref<File | null>(null)
const audio = ref<File | null>(null)
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

function selectAudio(selected: File | null): void {
  audio.value = selected
  logicError.value = null
  logicWarnings.value = []
}

async function exportLogic(): Promise<void> {
  if (!file.value) {
    return
  }

  logicBusy.value = true
  logicError.value = null
  logicWarnings.value = []
  try {
    const exported = await exportLogicProject(file.value, audio.value, toConversionOptions(form.value), outputName.value)
    logicWarnings.value = exported.warnings
    download(exported.zip, exported.fileName)
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
  form.value = defaultFormState()
  clearFormState()
  outputName.value = 'score'
  result.value = null
  stale.value = false
  error.value = null
  logicBusy.value = false
  logicError.value = null
  logicWarnings.value = []
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
    result.value = await convertScore(file.value, toConversionOptions(form.value), controller.signal)
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
      <button type="button" class="button secondary small" :title="t('resetTitle')" @click="reset">{{ t('reset') }}</button>
      <div class="locale" role="group" aria-label="Language">
      <button type="button" :aria-pressed="locale === 'de'" @click="setLocale('de')">DE</button>
      <button type="button" :aria-pressed="locale === 'en'" @click="setLocale('en')">EN</button>
      </div>
    </div>
  </header>

  <main>
    <section class="card">
      <h2>{{ t('scoreTitle') }}</h2>
      <FileDropZone
        :file="file"
        extension=".abc"
        accept=".abc,text/plain,text/vnd.abc"
        :drop-hint="t('dropHint')"
        :wrong-type-hint="t('notAbc')"
        @select="selectFile"
        @clear="file = null"
      />
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
    </section>

    <section class="card">
      <h2>{{ t('optionsTitle') }}</h2>
      <OptionsForm v-model="form" />

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

    <ResultView
      v-if="result"
      :result="result"
      :output-name="outputName"
      :stale="stale"
      :has-audio="audio !== null"
      :logic-busy="logicBusy"
      :logic-error="logicError"
      :logic-warnings="logicWarnings"
      @export-logic="exportLogic"
    />
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
