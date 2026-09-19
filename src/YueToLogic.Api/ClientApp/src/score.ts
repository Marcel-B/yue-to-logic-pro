import type { ConversionResult, ScoreDocument } from './types'

/** 1-based bar at a tick position, honouring meter changes (same rule as ScoreDocument.GetBarPosition). */
export function barAt(score: ScoreDocument, ticks: number): number {
  let barsBefore = 0
  const signatures = score.timeSignatures
  for (let i = 0; i < signatures.length; i++) {
    const signature = signatures[i]!
    const measureTicks = ((4 * score.ticksPerQuarterNote) / signature.denominator) * signature.numerator
    const segmentEnd = signatures[i + 1]?.startTicks ?? Number.POSITIVE_INFINITY
    if (ticks < segmentEnd) {
      return barsBefore + Math.floor(Math.max(0, ticks - signature.startTicks) / measureTicks) + 1
    }
    barsBefore += Math.ceil((segmentEnd - signature.startTicks) / measureTicks)
  }
  return 1
}

export function barCount(score: ScoreDocument): number {
  return barAt(score, score.lengthTicks) - 1
}

/** "score.abc" → "score"; the name the downloads are offered under. */
export function baseName(fileName: string): string {
  const dot = fileName.lastIndexOf('.')
  return dot > 0 ? fileName.slice(0, dot) : fileName
}

export function midiBlob(result: ConversionResult): Blob | null {
  if (!result.midi) {
    return null
  }
  const binary = atob(result.midi)
  const bytes = new Uint8Array(binary.length)
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i)
  }
  return new Blob([bytes], { type: 'audio/midi' })
}

/** The same dump the CLI writes with --dump-json: everything except the MIDI bytes. */
export function jsonBlob(result: ConversionResult): Blob {
  return new Blob([JSON.stringify({ ...result, midi: null }, null, 2)], { type: 'application/json' })
}

export function download(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.append(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), 0)
}
