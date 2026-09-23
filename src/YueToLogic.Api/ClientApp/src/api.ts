import type {
  Assignments,
  ConversionOptions,
  ConversionResult,
  Diagnostic,
  Instrument,
  InstrumentInput,
  LogicInstrument,
  MidiToAbcOptions,
  MidiToAbcResult,
  ReferenceVoice,
  SeparationModel,
  StemJob,
  VoiceJob,
  VoiceJobPage,
} from './types'

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

/**
 * The way back: a MIDI file, e.g. exported from Logic after editing the project, into a score.abc for YuE2.
 * Resolves for both 200 and 422 like `convertScore`: a file that became no score still lists its tracks, so
 * that their roles can be chosen by hand.
 */
export async function convertMidi(file: File, options: MidiToAbcOptions, signal?: AbortSignal): Promise<MidiToAbcResult> {
  const form = new FormData()
  form.append('file', file)
  form.append('options', JSON.stringify(options))

  let response: Response
  try {
    response = await fetch(`${apiBase}/api/midi/abc`, { method: 'POST', body: form, signal })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error
    }
    throw new ApiError('network', 0)
  }

  if (response.ok || response.status === 422) {
    return (await response.json()) as MidiToAbcResult
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
  /** A finished stem job, whose stems the server then puts on the project's own audio tracks. */
  stemJob: string | null,
  /** A finished voice job, whose converted vocals then take the project's vocals track. */
  voiceJob: string | null,
  /** The instrument each track plays; the track then sits on its channel and is named after it. */
  instruments: Record<string, LogicInstrument> = {},
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
  if (stemJob) {
    form.append('stemJob', stemJob)
  }
  if (voiceJob) {
    form.append('voiceJob', voiceJob)
  }
  if (Object.keys(instruments).length > 0) {
    form.append('instruments', JSON.stringify(instruments))
  }

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

/**
 * The separation models the service offers. Which ones there are is the gateway's business, so the list is
 * fetched rather than known here; it answers even while the separating Mac is away.
 */
export async function listStemModels(signal?: AbortSignal): Promise<SeparationModel[]> {
  return (await request('/api/stems/models', { signal })).json() as Promise<SeparationModel[]>
}

/**
 * Hands the recording over; the separation then runs for minutes on the stem service.
 *
 * @param model The id of a separation model, or an empty string for the service's default.
 */
export async function startStemJob(audio: File, dereverb: boolean, model: string, signal?: AbortSignal): Promise<StemJob> {
  return (await request(`/api/stems?dereverb=${dereverb}${model ? `&model=${encodeURIComponent(model)}` : ''}`, {
    method: 'POST',
    body: audio,
    headers: { 'Content-Type': 'audio/flac' },
    signal,
  })).json() as Promise<StemJob>
}

export async function stemJobStatus(id: string, signal?: AbortSignal): Promise<StemJob> {
  return (await request(`/api/stems/${id}`, { signal })).json() as Promise<StemJob>
}

export async function downloadStems(id: string): Promise<Blob> {
  return (await request(`/api/stems/${id}/result`)).blob()
}

/** Confirms the import, whereupon the stem service drops the result. Failure here is not worth reporting. */
export async function confirmStems(id: string): Promise<void> {
  await request(`/api/stems/${id}`, { method: 'DELETE' }).catch(() => undefined)
}

/** Every job the stem service knows, newest first: what is holding its queue up. */
export async function listStemJobs(signal?: AbortSignal): Promise<StemJob[]> {
  return (await request('/api/stems/jobs', { signal })).json() as Promise<StemJob[]>
}

/**
 * Removes a job from the stem service: cancels one that is still queued, drops the result of a finished one.
 * Unlike `confirmStems` this reports failure, since the user asked for it: 409 while the job is being
 * transferred to the Mac, 404 when it is gone already.
 */
export async function deleteStemJob(id: string): Promise<void> {
  await request(`/api/stems/${id}`, { method: 'DELETE' })
}

// ---- Voices ------------------------------------------------------------------------------------

/** Whether this server can have a voice changed at all. */
export async function voiceAvailable(): Promise<boolean> {
  try {
    const response = await fetch(`${apiBase}/api/voice`)
    return response.ok && ((await response.json()) as { available: boolean }).available
  } catch {
    return false
  }
}

/** The collection of reference voices; one of them is what a conversion takes its timbre from. */
export async function listVoices(signal?: AbortSignal): Promise<ReferenceVoice[]> {
  return (await request('/api/voice/voices', { signal })).json() as Promise<ReferenceVoice[]>
}

/** Stores a recording under a name; the service keeps the first 25 seconds, which is all the model uses. */
export async function addVoice(label: string, file: File): Promise<ReferenceVoice> {
  const form = new FormData()
  form.append('label', label)
  form.append('file', file)
  return (await request('/api/voice/voices', { method: 'POST', body: form })).json() as Promise<ReferenceVoice>
}

/**
 * The recording of a reference voice as the service keeps it - mono, 44.1 kHz, at most 25 seconds, what the
 * model gets - to listen to it again. 501 from a service too old to hand it out.
 */
export async function downloadVoiceAudio(id: string): Promise<Blob> {
  return (await request(`/api/voice/voices/${encodeURIComponent(id)}/audio`)).blob()
}

/** Removes a reference voice; the service refuses while a job still waits for it (409). */
export async function deleteVoice(id: string): Promise<void> {
  await request(`/api/voice/voices/${encodeURIComponent(id)}`, { method: 'DELETE' })
}

/**
 * Where the vocals of a conversion come from: a finished separation, whose vocal stem stays on the servers -
 * the browser passes the id, the stem travels from the stem service to this one and on - or a WAV of vocals
 * the user brings along, which needs no separation first.
 */
export type VoiceSource = { stemJob: string } | { file: File }

/** Converts vocals to a reference voice. */
export async function startVoiceJob(source: VoiceSource, voiceId: string, signal?: AbortSignal): Promise<VoiceJob> {
  const form = new FormData()
  if ('stemJob' in source) {
    form.append('stemJob', source.stemJob)
  } else {
    form.append('file', source.file, source.file.name)
  }
  form.append('voiceId', voiceId)
  return (await request('/api/voice/jobs', { method: 'POST', body: form, signal })).json() as Promise<VoiceJob>
}

export async function voiceJobStatus(id: string, signal?: AbortSignal): Promise<VoiceJob> {
  return (await request(`/api/voice/jobs/${encodeURIComponent(id)}`, { signal })).json() as Promise<VoiceJob>
}

/** The converted recording as a WAV, for whoever wants it beside the project. */
export async function downloadVoiceResult(id: string): Promise<Blob> {
  return (await request(`/api/voice/jobs/${encodeURIComponent(id)}/result`)).blob()
}

/**
 * A page of the voice service's jobs, newest first. A service without that route answers 501, which the
 * interface shows as "this one cannot list its jobs" rather than as an empty list.
 *
 * @param status Only jobs in that state, or an empty string for all of them.
 */
export async function listVoiceJobs(
  limit: number,
  offset: number,
  status = '',
  signal?: AbortSignal,
): Promise<VoiceJobPage> {
  const query = new URLSearchParams({ limit: String(limit), offset: String(offset) })
  if (status) {
    query.set('status', status)
  }
  return (await request(`/api/voice/jobs?${query}`, { signal })).json() as Promise<VoiceJobPage>
}

/** Cancels a job or drops a finished one's result. Failure is not worth reporting when nobody asked. */
export async function forgetVoiceJob(id: string): Promise<void> {
  await request(`/api/voice/jobs/${encodeURIComponent(id)}`, { method: 'DELETE' }).catch(() => undefined)
}

/** The same call where the user asked for it, so a refusal is worth showing. */
export async function deleteVoiceJob(id: string): Promise<void> {
  await request(`/api/voice/jobs/${encodeURIComponent(id)}`, { method: 'DELETE' })
}

// ---- Presets -----------------------------------------------------------------------------------

/** A preset as the server keeps it; the form is whatever the interface saved, checked on the way in. */
export interface StoredPreset {
  id: number
  name: string
  form: unknown
  updatedAt: string
}

export async function listPresets(): Promise<StoredPreset[]> {
  return (await request('/api/presets')).json() as Promise<StoredPreset[]>
}

/** Saves the form under the name, replacing a preset of that name. */
export async function putPreset(name: string, form: unknown): Promise<StoredPreset> {
  return (await request(`/api/presets/${encodeURIComponent(name)}`, json('PUT', { form }))).json() as Promise<StoredPreset>
}

export async function removePreset(name: string): Promise<void> {
  await request(`/api/presets/${encodeURIComponent(name)}`, { method: 'DELETE' })
}

// ---- Instruments -------------------------------------------------------------------------------

export async function listInstruments(): Promise<Instrument[]> {
  return (await request('/api/instruments')).json() as Promise<Instrument[]>
}

export async function createInstrument(input: InstrumentInput): Promise<Instrument> {
  return (await request('/api/instruments', json('POST', input))).json() as Promise<Instrument>
}

export async function updateInstrument(id: number, input: InstrumentInput): Promise<Instrument> {
  return (await request(`/api/instruments/${id}`, json('PUT', input))).json() as Promise<Instrument>
}

/** Removes the instrument; the server drops the assignments of tracks to it as well. */
export async function deleteInstrument(id: number): Promise<void> {
  await request(`/api/instruments/${id}`, { method: 'DELETE' })
}

export async function listAssignments(): Promise<Assignments> {
  return (await request('/api/instruments/assignments')).json() as Promise<Assignments>
}

/** Gives a track an instrument, or takes it away with `null`. */
export async function assignInstrument(track: string, instrumentId: number | null): Promise<void> {
  await request(`/api/instruments/assignments/${encodeURIComponent(track)}`, json('PUT', { instrumentId }))
}

function json(method: string, body: unknown): RequestInit {
  return { method, body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } }
}

/** A request whose failure is worth an ApiError: a problem document's detail, or the status. */
async function request(path: string, init: RequestInit = {}): Promise<Response> {
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
