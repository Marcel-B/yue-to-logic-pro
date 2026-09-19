import type { ConversionOptions, ConversionResult, Diagnostic } from './types'

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

/** Builds a zipped Logic Pro project (.logicx) from the score, its audio.flac and the options. */
export async function exportLogicProject(
  file: File,
  audio: File,
  options: ConversionOptions,
  name: string,
  signal?: AbortSignal,
): Promise<LogicExport> {
  const form = new FormData()
  form.append('file', file)
  form.append('audio', audio)
  form.append('options', JSON.stringify(options))
  form.append('name', name)

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
