<script setup lang="ts">
import { ref, watch } from 'vue'
import { ApiError, convertScore } from './api'
import OptionsForm from './components/OptionsForm.vue'
import ResultView from './components/ResultView.vue'
import ScoreDropZone from './components/ScoreDropZone.vue'
import { locale, setLocale, t } from './i18n'
import { loadFormState, saveFormState, toConversionOptions } from './options'
import { baseName } from './score'
import type { ConversionResult } from './types'

const file = ref<File | null>(null)
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
    <div class="locale" role="group" aria-label="Language">
      <button type="button" :aria-pressed="locale === 'de'" @click="setLocale('de')">DE</button>
      <button type="button" :aria-pressed="locale === 'en'" @click="setLocale('en')">EN</button>
    </div>
  </header>

  <main>
    <section class="card">
      <h2>{{ t('scoreTitle') }}</h2>
      <ScoreDropZone :file="file" @select="selectFile" />
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

    <ResultView v-if="result" :result="result" :output-name="outputName" :stale="stale" />
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
