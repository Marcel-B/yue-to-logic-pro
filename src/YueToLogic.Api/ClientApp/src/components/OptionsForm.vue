<script setup lang="ts">
import { t } from '../i18n'
import type { FormState } from '../options'

const form = defineModel<FormState>({ required: true })

const octaves = [-4, -3, -2, -1, 0, 1, 2, 3, 4]
const bassOctaves = [-2, -1, 0, 1, 2]

function signed(value: number): string {
  return value > 0 ? `+${value}` : String(value)
}
</script>

<template>
  <div class="options">
    <fieldset>
      <legend>{{ t('tracks') }}</legend>
      <label class="check">
        <input v-model="form.includeChords" type="checkbox" />
        {{ t('includeChords') }}
      </label>
    </fieldset>

    <fieldset>
      <legend>{{ t('octaves') }}</legend>
      <div class="row">
        <label>
          {{ t('octaveBoth') }}
          <select v-model.number="form.octave">
            <option v-for="value in octaves" :key="value" :value="value">{{ signed(value) }}</option>
          </select>
        </label>
        <label>
          {{ t('octaveVocal') }}
          <select v-model="form.vocalOctave">
            <option :value="null">{{ t('sameAsBoth') }}</option>
            <option v-for="value in octaves" :key="value" :value="value">{{ signed(value) }}</option>
          </select>
        </label>
        <label>
          {{ t('octaveIns') }}
          <select v-model="form.insOctave">
            <option :value="null">{{ t('sameAsBoth') }}</option>
            <option v-for="value in octaves" :key="value" :value="value">{{ signed(value) }}</option>
          </select>
        </label>
      </div>
    </fieldset>

    <fieldset>
      <legend>{{ t('bass') }}</legend>
      <div class="row">
        <label class="grow">
          <span class="sr-only">{{ t('bass') }}</span>
          <select v-model="form.bass">
            <option value="off">{{ t('bassOff') }}</option>
            <option value="Eighths">{{ t('bassEighths') }}</option>
            <option value="Quarters">{{ t('bassQuarters') }}</option>
            <option value="RootFifth">{{ t('bassRootFifth') }}</option>
          </select>
        </label>
        <label>
          {{ t('bassOctave') }}
          <select v-model.number="form.bassOctave" :disabled="form.bass === 'off'">
            <option v-for="value in bassOctaves" :key="value" :value="value">{{ signed(value) }}</option>
          </select>
        </label>
      </div>
    </fieldset>

    <fieldset>
      <legend>{{ t('drums') }}</legend>
      <label class="check">
        <input v-model="form.drums" type="checkbox" />
        {{ t('drumsOn') }}
      </label>
      <label class="check" :class="{ disabled: !form.drums }">
        <input v-model="form.crash" type="checkbox" :disabled="!form.drums" />
        {{ t('crash') }}
      </label>
    </fieldset>

    <details>
      <summary>{{ t('advanced') }}</summary>
      <label class="inline">
        {{ t('ppq') }}
        <input v-model.number="form.ppq" type="number" min="24" max="32767" step="24" />
      </label>
    </details>
  </div>
</template>

<style scoped>
.options {
  display: grid;
  gap: 1rem;
}

fieldset {
  display: grid;
  gap: 0.5rem;
  margin: 0;
  padding: 0;
  border: 0;
}

legend {
  margin-bottom: 0.4rem;
  padding: 0;
  font-weight: 600;
}

.row {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-end;
  gap: 0.75rem;
}

.row label {
  display: grid;
  gap: 0.25rem;
  min-width: 8.5rem;
  color: var(--text-muted);
  font-size: 0.875rem;
}

.row .grow {
  flex: 1 1 14rem;
}

.check {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.check.disabled {
  color: var(--text-muted);
}

.inline {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 0.75rem;
  margin-top: 0.75rem;
}

.inline input {
  width: 7rem;
}

summary {
  cursor: pointer;
  font-weight: 600;
}
</style>
