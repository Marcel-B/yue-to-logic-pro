import { AUDIO_OUTPUT, type MidiPort, type Routing } from './player'
import type { Assignments, ConversionOptions, Instrument, LogicInstrument } from './types'

/**
 * Instruments are what the user calls a MIDI port and channel: "Mother32" rather than "MIDI4x4 Midi Out 1,
 * channel 12". They live on the server, as does which track plays which. This module turns that into what
 * the preview and the exports work with.
 */

/** The instrument a track is assigned to, or null when it has none or the instrument is gone. */
export function instrumentOf(track: string, assignments: Assignments, instruments: Instrument[]): Instrument | null {
  const id = assignments[track]
  return id === undefined ? null : (instruments.find((instrument) => instrument.id === id) ?? null)
}

/**
 * The port an instrument names, among those the browser currently offers. Ports are matched by name because
 * the id Web MIDI gives them differs between machines, while the name is what the device is called.
 */
export function resolvePort(ports: MidiPort[], portName: string): MidiPort | null {
  const wanted = portName.trim()
  return ports.find((port) => port.name.trim() === wanted) ?? null
}

/** Where a track really goes: its instrument, if it has one, else the routing chosen by hand. */
export interface EffectiveRouting {
  routing: Routing
  instrument: Instrument | null
  /** The instrument's port, when it is connected; a null with an instrument means it is not. */
  port: MidiPort | null
}

/**
 * An instrument overrides the port and channel of a routing but not whether the track is muted. An instrument
 * whose port is unplugged plays through the browser sound, so that the preview still sounds.
 */
export function effectiveRouting(routing: Routing, instrument: Instrument | null, ports: MidiPort[]): EffectiveRouting {
  if (!instrument) {
    return { routing, instrument: null, port: null }
  }
  const port = resolvePort(ports, instrument.port)
  return {
    routing: { output: port?.id ?? AUDIO_OUTPUT, channel: instrument.channel - 1, muted: routing.muted },
    instrument,
    port,
  }
}

/** Every assigned track with its instrument; assignments to instruments that no longer exist are left out. */
function assigned(assignments: Assignments, instruments: Instrument[]): [string, Instrument][] {
  return Object.keys(assignments).flatMap((track) => {
    const instrument = instrumentOf(track, assignments, instruments)
    return instrument ? [[track, instrument] as [string, Instrument]] : []
  })
}

/**
 * The instrument's channel wins over the one chosen under "Advanced", so that the MIDI file, the preview and
 * the Logic project all play a track where its instrument listens.
 */
export function withInstrumentChannels(
  options: ConversionOptions,
  assignments: Assignments,
  instruments: Instrument[],
): ConversionOptions {
  const midiChannels = { ...options.midiChannels }
  for (const [track, instrument] of assigned(assignments, instruments)) {
    midiChannels[track] = instrument.channel
  }
  return { ...options, midiChannels }
}

/** What the Logic export needs to know per track: the instrument's name, port and channel. */
export function instrumentsForExport(assignments: Assignments, instruments: Instrument[]): Record<string, LogicInstrument> {
  return Object.fromEntries(
    assigned(assignments, instruments).map(([track, { name, port, channel }]) => [track, { name, port, channel }]),
  )
}
