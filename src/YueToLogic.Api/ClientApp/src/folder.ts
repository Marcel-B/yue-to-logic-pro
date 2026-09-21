/**
 * Reads a dropped YuE output folder. A run holds one folder per song (`song1`, `song2`, …), each with a
 * `score.abc` and the `audio.flac` that belongs to it, so the pair is looked for at any depth and the songs
 * are returned in the order their folders are named.
 */
export interface SongFolder {
  /** The folder the pair was found in, for telling songs apart in the interface. */
  name: string
  score: File
  audio: File | null
}

/** How deep a dropped folder is walked; a run nests one level, the rest is a safeguard. */
const maxDepth = 4

/** Whether the drag carries a folder rather than files, which only the entry API can tell. */
export function hasFolder(transfer: DataTransfer | null): boolean {
  return [...(transfer?.items ?? [])].some((item) => item.webkitGetAsEntry()?.isDirectory === true)
}

/** The songs of a dropped folder; empty when the drop holds no score at all. */
export async function readSongFolders(transfer: DataTransfer): Promise<SongFolder[]> {
  const roots = [...transfer.items].map((item) => item.webkitGetAsEntry()).filter((entry) => entry !== null)
  const songs: SongFolder[] = []
  for (const root of roots) {
    await collect(root, songs, 0)
  }
  return songs.sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true }))
}

/** The songs of a folder chosen through the file dialog, which hands over its files with their paths. */
export function readSongFiles(files: FileList | File[]): SongFolder[] {
  const byFolder = new Map<string, File[]>()
  for (const file of files) {
    const path = (file as File & { webkitRelativePath?: string }).webkitRelativePath ?? file.name
    const folder = path.slice(0, path.lastIndexOf('/'))
    byFolder.set(folder, [...(byFolder.get(folder) ?? []), file])
  }

  const songs: SongFolder[] = []
  for (const [folder, files] of byFolder) {
    const song = pair(folder.slice(folder.lastIndexOf('/') + 1), files)
    if (song) {
      songs.push(song)
    }
  }
  return songs.sort((a, b) => a.name.localeCompare(b.name, undefined, { numeric: true }))
}

async function collect(entry: FileSystemEntry, songs: SongFolder[], depth: number): Promise<void> {
  if (entry.isFile) {
    return
  }

  const directory = entry as FileSystemDirectoryEntry
  const entries = await read(directory)
  const files = await Promise.all(entries.filter((e) => e.isFile).map((e) => file(e as FileSystemFileEntry)))
  const song = pair(entry.name, files)
  if (song) {
    songs.push(song)
  }

  if (depth < maxDepth) {
    for (const child of entries.filter((e) => e.isDirectory)) {
      await collect(child, songs, depth + 1)
    }
  }
}

/** A score with the audio beside it; YuE names them score.abc and audio.flac. */
function pair(name: string, files: File[]): SongFolder | null {
  const score = files.find((f) => f.name.toLowerCase().endsWith('.abc'))
  return score ? { name, score, audio: files.find((f) => f.name.toLowerCase().endsWith('.flac')) ?? null } : null
}

function read(directory: FileSystemDirectoryEntry): Promise<FileSystemEntry[]> {
  const reader = directory.createReader()
  const all: FileSystemEntry[] = []
  // readEntries hands out a batch at a time and signals the end with an empty one.
  return new Promise((resolve, reject) => {
    const next = (): void =>
      reader.readEntries((batch) => {
        if (batch.length === 0) {
          resolve(all)
          return
        }
        all.push(...batch)
        next()
      }, reject)
    next()
  })
}

function file(entry: FileSystemFileEntry): Promise<File> {
  return new Promise((resolve, reject) => entry.file(resolve, reject))
}
