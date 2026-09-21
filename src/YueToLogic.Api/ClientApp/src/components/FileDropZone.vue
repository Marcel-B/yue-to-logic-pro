<script setup lang="ts">
import { computed, ref } from 'vue'
import { hasFolder, readSongFiles, readSongFolders, type SongFolder } from '../folder'
import { t } from '../i18n'

const props = defineProps<{
  file: File | null
  /** Whether a whole YuE output folder can be dropped here, which fills score and audio at once. */
  folders?: boolean
  /** Expected file extension, e.g. ".abc"; other files are accepted but flagged. */
  extension: string
  accept: string
  dropHint: string
  wrongTypeHint: string
}>()
const emit = defineEmits<{ select: [file: File]; songs: [songs: SongFolder[]]; clear: [] }>()

const input = ref<HTMLInputElement | null>(null)
const folderInput = ref<HTMLInputElement | null>(null)
const dragDepth = ref(0)
const dragging = computed(() => dragDepth.value > 0)
const expectedType = computed(() => !props.file || props.file.name.toLowerCase().endsWith(props.extension))

function openDialog(): void {
  input.value?.click()
}

function openFolderDialog(): void {
  folderInput.value?.click()
}

function onInput(event: Event): void {
  const target = event.target as HTMLInputElement
  const files = [...(target.files ?? [])]
  if (target.webkitdirectory) {
    emit('songs', readSongFiles(files))
  } else if (files[0]) {
    emit('select', files[0])
  }
  // Allows choosing the same file again after editing it on disk.
  target.value = ''
}

async function onDrop(event: DragEvent): Promise<void> {
  dragDepth.value = 0
  const transfer = event.dataTransfer
  if (props.folders && hasFolder(transfer)) {
    emit('songs', await readSongFolders(transfer!))
    return
  }

  const file = transfer?.files[0]
  if (file) {
    emit('select', file)
  }
}

function formatSize(bytes: number): string {
  if (bytes < 1024) {
    return `${bytes} B`
  }
  return bytes < 1024 * 1024 ? `${(bytes / 1024).toFixed(1)} KB` : `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}
</script>

<template>
  <div
    class="drop-zone"
    :class="{ dragging, filled: file }"
    role="button"
    tabindex="0"
    :aria-label="t('chooseFile')"
    @click="openDialog"
    @keydown.enter.prevent="openDialog"
    @keydown.space.prevent="openDialog"
    @dragenter.prevent="dragDepth++"
    @dragover.prevent
    @dragleave.prevent="dragDepth = Math.max(0, dragDepth - 1)"
    @drop.prevent="onDrop"
  >
    <input ref="input" type="file" :accept="accept" hidden @change="onInput" />
    <input v-if="folders" ref="folderInput" type="file" webkitdirectory hidden @change="onInput" />

    <template v-if="dragging">
      <p class="headline">{{ t('dropWhileDragging') }}</p>
    </template>
    <template v-else-if="file">
      <p class="headline file-name">{{ file.name }}</p>
      <p class="muted">{{ formatSize(file.size) }}</p>
      <span class="links">
        <span class="link">{{ t('otherFile') }}</span>
        <button type="button" class="link" @click.stop="emit('clear')">{{ t('removeFile') }}</button>
      </span>
    </template>
    <template v-else>
      <svg class="icon" viewBox="0 0 24 24" aria-hidden="true">
        <path d="M12 16V4m0 0L7 9m5-5 5 5M5 16v3a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1v-3" />
      </svg>
      <p class="headline">{{ dropHint }}</p>
      <p class="muted">{{ t('dropOr') }}</p>
      <span class="links buttons">
        <span class="button secondary">{{ t('chooseFile') }}</span>
        <button v-if="folders" type="button" class="button secondary" @click.stop="openFolderDialog">
          {{ t('chooseFolder') }}
        </button>
      </span>
    </template>
  </div>
  <p v-if="!expectedType" class="hint warning">{{ wrongTypeHint }}</p>
</template>

<style scoped>
.drop-zone {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.35rem;
  padding: 2rem 1rem;
  border: 2px dashed var(--border-strong);
  border-radius: var(--radius);
  background: var(--surface-sunken);
  text-align: center;
  cursor: pointer;
  transition:
    border-color 0.15s,
    background 0.15s;
}

.drop-zone:hover,
.drop-zone:focus-visible {
  border-color: var(--accent);
  outline: none;
}

.drop-zone.dragging {
  border-color: var(--accent);
  background: var(--accent-soft);
}

.drop-zone.filled {
  border-style: solid;
  padding: 1.25rem 1rem;
}

.headline {
  margin: 0;
  font-weight: 600;
}

.file-name {
  overflow-wrap: anywhere;
}

.muted {
  margin: 0;
}

.buttons {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 0.5rem;
}

.links {
  display: flex;
  gap: 1rem;
}

.icon {
  width: 2.25rem;
  height: 2.25rem;
  fill: none;
  stroke: var(--accent);
  stroke-width: 1.8;
  stroke-linecap: round;
  stroke-linejoin: round;
}

.button {
  margin-top: 0.4rem;
}
</style>
