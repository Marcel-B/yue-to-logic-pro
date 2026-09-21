import type { ConversionOptions, ConversionResult, Diagnostic, StemJob } from './types'

const apiBase = import.meta.env.VITE_API_BASE ?? ''

/** A request the backend refused or could not answer; `status` is 0 for network errors. */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message)
  }
}

/**
 * Sends the score and options to the backend. Resolves with the result for both a successful (200) and an
 * unconvertible (422) score, because both carry diagnostics worth showing; rejects with an ApiError otherwise.
 */
export async function convertScore(file: File, options: ConversionOptions, signal?: AbortSignal): Promise<ConversionResult> {
  const form = new FormData()
  form.append('file', file)
  form.append('options', JSON.stringify(options))

  let response: Response
  try {
    response = await fetch(`${apiBase}/api/convert`, { method: 'POST', body: form, signal })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error
    }
    throw new ApiError('network', 0)
  }

  if (response.ok || response.status === 422) {
    return (await response.json()) as ConversionResult
  }

  const problem = (await response.json().catch(() => null)) as { title?: string; detail?: string } | null
  throw new ApiError(problem?.detail ?? problem?.title ?? `HTTP ${response.status}`, response.status)
}

/** The Logic project could not be built; the diagnostics say why (e.g. not a FLAC file). */
export class LogicExportError extends Error {
  constructor(readonly diagnostics: Diagnostic[]) {
    super(diagnostics.map((d) => d.message).join(' '))
  }
}

export interface LogicExport {
  zip: Blob
  fileName: string
  /** Warnings such as an audio file whose length does not match the score. */
  warnings: Diagnostic[]
}

/** Builds a zipped Logic Pro project (.logicx) from the score, the options and, if given, its audio.flac. */
export async function exportLogicProject(
  file: File,
  audio: File | null,
  options: ConversionOptions,
  name: string,
  /** One region per song section instead of one per track; a property of the project, not of the score. */
  splitSections: boolean,
  signal?: AbortSignal,
): Promise<LogicExport> {
  const form = new FormData()
  form.append('file', file)
  if (audio) {
    form.append('audio', audio)
  }
  form.append('options', JSON.stringify(options))
  form.append('name', name)
  form.append('splitSections', String(splitSections))

  let response: Response
  try {
    response = await fetch(`${apiBase}/api/convert/logic`, { method: 'POST', body: form, signal })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error
    }
    throw new ApiError('network', 0)
  }

  if (response.status === 422) {
    throw new LogicExportError(((await response.json()) as ConversionResult).diagnostics)
  }

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as { title?: string; detail?: string } | null
    throw new ApiError(problem?.detail ?? problem?.title ?? `HTTP ${response.status}`, response.status)
  }

  const header = response.headers.get('X-YueToLogic-Diagnostics')
  return {
    zip: await response.blob(),
    fileName: `${name}.logicx.zip`,
    warnings: header ? (JSON.parse(header) as Diagnostic[]) : [],
  }
}

// ---- Stems -------------------------------------------------------------------------------------

/** Whether this server can have a recording separated into stems at all. */
export async function stemsAvailable(): Promise<boolean> {
  try {
    const response = await fetch(`${apiBase}/api/stems`)
    return response.ok && ((await response.json()) as { available: boolean }).available
  } catch {
    return false
  }
}

/** Hands the recording over; the separation then runs for minutes on the stem service. */
export async function startStemJob(audio: File, dereverb: boolean, signal?: AbortSignal): Promise<StemJob> {
  return (await stemRequest(`/api/stems?dereverb=${dereverb}`, {
    method: 'POST',
    body: audio,
    headers: { 'Content-Type': 'audio/flac' },
    signal,
  })).json() as Promise<StemJob>
}

export async function stemJobStatus(id: string, signal?: AbortSignal): Promise<StemJob> {
  return (await stemRequest(`/api/stems/${id}`, { signal })).json() as Promise<StemJob>
}

export async function downloadStems(id: string): Promise<Blob> {
  return (await stemRequest(`/api/stems/${id}/result`)).blob()
}

/** Confirms the import, whereupon the stem service drops the result. Failure here is not worth reporting. */
export async function confirmStems(id: string): Promise<void> {
  await stemRequest(`/api/stems/${id}`, { method: 'DELETE' }).catch(() => undefined)
}

async function stemRequest(path: string, init: RequestInit = {}): Promise<Response> {
  let response: Response
  try {
    response = await fetch(`${apiBase}${path}`, init)
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error
    }
    throw new ApiError('network', 0)
  }

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as { title?: string; detail?: string } | null
    throw new ApiError(problem?.detail ?? problem?.title ?? `HTTP ${response.status}`, response.status)
  }
  return response
}
