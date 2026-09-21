<script setup lang="ts">
import { t } from '../i18n'
import type { FormState } from '../options'
import { TRACK_NAMES } from '../types'

const form = defineModel<FormState>({ required: true })

/** The tempo fit needs a recording to measure against, so the switch stays off without one. */
defineProps<{ hasAudio: boolean }>()

const octaves = [-4, -3, -2, -1, 0, 1, 2, 3, 4]
const bassOctaves = [-2, -1, 0, 1, 2]
const countInBars = [0, 1, 2, 4]
const channels = Array.from({ length: 16 }, (_, i) => i + 1)

/**
 * Channels and programs are kept only for the tracks that have one, so that saved settings stay small and
 * a track without a setting keeps following the converter.
 */
function assign(values: Record<string, number>, track: string, value: number): void {
  if (value > 0) {
    values[track] = value
  } else {
    delete values[track]
  }
}

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
      <label class="select-full" :class="{ disabled: !form.includeChords }">
        <span class="sr-only">{{ t('chordPattern') }}</span>
        <select v-model="form.chordPattern" :disabled="!form.includeChords">
          <option value="as-written">{{ t('chordsAsWritten') }}</option>
          <option value="Eighths">{{ t('chordsEighths') }}</option>
          <option value="Offbeat">{{ t('chordsOffbeat') }}</option>
          <option value="Sixteenths">{{ t('chordsSixteenths') }}</option>
          <option value="ArpeggioUp">{{ t('chordsArpeggio') }}</option>
          <option value="ArpeggioUpDown">{{ t('chordsArpeggioUpDown') }}</option>
        </select>
      </label>
      <div class="row">
        <label class="grow">
          {{ t('chordInversion') }}
          <select v-model="form.chordInversion" :disabled="!form.includeChords">
            <option value="RootPosition">{{ t('chordRoot') }}</option>
            <option value="Closest">{{ t('chordClosest') }}</option>
            <option value="First">{{ t('chordFirst') }}</option>
            <option value="Second">{{ t('chordSecond') }}</option>
          </select>
        </label>
        <label>
          {{ t('chordOctave') }}
          <select v-model.number="form.chordOctave" :disabled="!form.includeChords">
            <option v-for="value in bassOctaves" :key="value" :value="value">{{ signed(value) }}</option>
          </select>
        </label>
      </div>
      <label class="check">
        <input v-model="form.guideTones" type="checkbox" />
        {{ t('guideTones') }}
      </label>
      <label class="check">
        <input v-model="form.doubleVocal" type="checkbox" />
        {{ t('doubleVocal') }}
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
        <label :class="{ disabled: !form.guideTones }">
          {{ t('guideOctave') }}
          <select v-model.number="form.guideOctave" :disabled="!form.guideTones">
            <option v-for="value in bassOctaves" :key="value" :value="value">{{ signed(value) }}</option>
          </select>
        </label>
        <label :class="{ disabled: !form.doubleVocal }">
          {{ t('doubleOctave') }}
          <select v-model.number="form.doubleOctave" :disabled="!form.doubleVocal">
            <option v-for="value in bassOctaves" :key="value" :value="value">{{ signed(value) }}</option>
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
            <option value="Octaves">{{ t('bassOctaves') }}</option>
            <option value="Offbeat">{{ t('bassOffbeat') }}</option>
            <option value="Sustained">{{ t('bassSustained') }}</option>
            <option value="Walking">{{ t('bassWalking') }}</option>
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
      <label class="select-full">
        <span class="sr-only">{{ t('drums') }}</span>
        <select v-model="form.drums">
          <option value="off">{{ t('drumsOff') }}</option>
          <option value="FourOnTheFloor">{{ t('drumsOn') }}</option>
          <option value="Backbeat">{{ t('drumsBackbeat') }}</option>
          <option value="HalfTime">{{ t('drumsHalfTime') }}</option>
          <option value="Disco">{{ t('drumsDisco') }}</option>
          <option value="SixteenthHats">{{ t('drumsSixteenthHats') }}</option>
          <option value="Shuffle">{{ t('drumsShuffle') }}</option>
        </select>
      </label>
      <label class="check" :class="{ disabled: form.drums === 'off' }">
        <input v-model="form.crash" type="checkbox" :disabled="form.drums === 'off'" />
        {{ t('crash') }}
      </label>
      <label class="check" :class="{ disabled: form.drums === 'off' }">
        <input v-model="form.splitDrums" type="checkbox" :disabled="form.drums === 'off'" />
        {{ t('splitDrums') }}
      </label>
    </fieldset>

    <fieldset>
      <legend>{{ t('groove') }}</legend>
      <div class="row">
        <label class="grow">
          {{ t('swing') }}
          <span class="slider">
            <input v-model.number="form.swing" type="range" min="0" max="100" step="5" />
            <output>{{ t('percentValue', { value: form.swing }) }}</output>
          </span>
        </label>
        <label>
          {{ t('swingUnit') }}
          <select v-model="form.swingUnit" :disabled="form.swing === 0">
            <option value="Eighths">{{ t('swingEighths') }}</option>
            <option value="Sixteenths">{{ t('swingSixteenths') }}</option>
          </select>
        </label>
      </div>
      <label class="check" :class="{ disabled: form.swing === 0 || form.drums === 'off' }">
        <input v-model="form.straightDrums" type="checkbox" :disabled="form.swing === 0 || form.drums === 'off'" />
        {{ t('straightDrums') }}
      </label>
      <div class="row">
        <label class="grow">
          {{ t('humanize') }}
          <span class="slider">
            <input v-model.number="form.humanize" type="range" min="0" max="100" step="5" />
            <output>{{ t('percentValue', { value: form.humanize }) }}</output>
          </span>
        </label>
      </div>
    </fieldset>

    <fieldset>
      <legend>{{ t('mono') }}</legend>
      <label class="check">
        <input v-model="form.mono" type="checkbox" />
        {{ t('monoPrepare') }}
      </label>
      <label class="check" :class="{ disabled: !form.mono }">
        <input v-model="form.legato" type="checkbox" :disabled="!form.mono" />
        {{ t('legato') }}
      </label>
    </fieldset>

    <fieldset>
      <legend>{{ t('countIn') }}</legend>
      <div class="row">
        <label>
          {{ t('countInBars') }}
          <select v-model.number="form.countIn">
            <option v-for="value in countInBars" :key="value" :value="value">
              {{ value === 0 ? t('countInOff') : value }}
            </option>
          </select>
        </label>
      </div>
      <label class="check" :class="{ disabled: form.countIn === 0 }">
        <input v-model="form.countInClick" type="checkbox" :disabled="form.countIn === 0" />
        {{ t('countInClick') }}
      </label>
    </fieldset>

    <fieldset>
      <legend>{{ t('logicProject') }}</legend>
      <label class="check">
        <input v-model="form.splitSections" type="checkbox" />
        {{ t('splitSections') }}
      </label>
      <label class="check" :class="{ disabled: !hasAudio }">
        <input v-model="form.fitTempo" type="checkbox" :disabled="!hasAudio" />
        {{ t('fitTempo') }}
      </label>
      <p class="muted hint">{{ hasAudio ? t('fitTempoHint') : t('fitTempoNeedsAudio') }}</p>
    </fieldset>

    <details>
      <summary>{{ t('advanced') }}</summary>
      <label class="inline">
        {{ t('ppq') }}
        <input v-model.number="form.ppq" type="number" min="24" max="32767" step="24" />
      </label>

      <p class="muted intro">{{ t('midiTracksInfo') }}</p>
      <table class="tracks">
        <thead>
          <tr>
            <th>{{ t('midiTrack') }}</th>
            <th>{{ t('midiChannel') }}</th>
            <th>{{ t('midiProgram') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="track in TRACK_NAMES" :key="track">
            <th scope="row">{{ track }}</th>
            <td>
              <select
                :value="form.channels[track] ?? 0"
                :aria-label="`${t('midiChannel')} ${track}`"
                @change="assign(form.channels, track, Number(($event.target as HTMLSelectElement).value))"
              >
                <option :value="0">{{ t('midiAuto') }}</option>
                <option v-for="channel in channels" :key="channel" :value="channel">{{ channel }}</option>
              </select>
            </td>
            <td>
              <input
                :value="form.programs[track] ?? ''"
                type="number"
                min="1"
                max="128"
                :placeholder="t('midiNone')"
                :aria-label="`${t('midiProgram')} ${track}`"
                @input="assign(form.programs, track, Number(($event.target as HTMLInputElement).value))"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </details>
  </div>
</template>

<style scoped>
/* On a wide screen the groups stand next to each other instead of stretching across the whole card. */
.options {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(20rem, 1fr));
  gap: 1rem 2rem;
  align-items: start;
}

.options > details {
  grid-column: 1 / -1;
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

/* A select is as wide as its longest option unless it is told otherwise, which would overflow the row. */
.row label select {
  width: 100%;
  min-width: 0;
}

.row .grow {
  flex: 1 1 14rem;
}

.select-full select {
  width: 100%;
}

/* A range and the value it stands at, so the percentage is readable while dragging. */
.slider {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.slider input {
  flex: 1 1 auto;
  min-width: 0;
}

.slider output {
  min-width: 3rem;
  text-align: right;
  font-variant-numeric: tabular-nums;
}

.row label.disabled {
  opacity: 0.6;
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

.tracks {
  width: 100%;
  margin-top: 0.5rem;
  border-collapse: collapse;
  font-size: 0.875rem;
}

.tracks th {
  color: var(--text-muted);
  font-weight: 500;
  text-align: left;
}

.tracks td,
.tracks th {
  padding: 0.15rem 0.5rem 0.15rem 0;
}

.tracks select,
.tracks input {
  width: 100%;
  min-width: 4rem;
}

summary {
  cursor: pointer;
  font-weight: 600;
}
</style>
