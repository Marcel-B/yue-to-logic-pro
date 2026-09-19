import type { ConversionOptions, ConversionResult } from './types'

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
