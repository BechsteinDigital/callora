import { beforeEach, describe, expect, it } from 'vitest'
import { backoffMs, connectSurfaceContextBridge } from './context-bridge'
import { createSurfaceContextChannel } from './surface-context-channel'

/**
 * A socket stand-in. The real one is injected, so these tests exercise the bridge's own
 * behaviour rather than happy-dom's WebSocket — which cannot connect to anything anyway.
 */
class FakeSocket {
  static last: FakeSocket | undefined

  onopen: (() => void) | null = null
  onmessage: ((event: MessageEvent) => void) | null = null
  onclose: (() => void) | null = null
  closed = false

  constructor(readonly url: string) {
    FakeSocket.last = this
  }

  close(): void {
    this.closed = true
  }

  receive(payload: unknown): void {
    this.onmessage?.({ data: JSON.stringify(payload) } as MessageEvent)
  }

  receiveRaw(data: string): void {
    this.onmessage?.({ data } as MessageEvent)
  }

  drop(): void {
    this.onclose?.()
  }
}

const KEY = 'communication.active-call/v1'

function bridge(channel = createSurfaceContextChannel('acme', 'agent-desk')) {
  const scheduled: { handler: () => void; delayMs: number }[] = []
  const instance = connectSurfaceContextBridge(channel, {
    path: '/agent-desk',
    createSocket: (url) => new FakeSocket(url) as unknown as WebSocket,
    schedule: (handler, delayMs) => scheduled.push({ handler, delayMs }),
  })

  return { instance, channel, scheduled, socket: () => FakeSocket.last! }
}

beforeEach(() => {
  FakeSocket.last = undefined
})

describe('surface context bridge', () => {
  it('hands a server value to the local channel', () => {
    const { channel, socket } = bridge()
    const seen: unknown[] = []
    channel.subscribe(KEY, (value) => seen.push(value))

    socket().receive({ key: KEY, value: { state: 'ringing' } })

    // Ein Abonnent ohne Snapshot wird beim Abonnieren NICHT aufgerufen — erst der Wert
    // löst aus. Sonst müsste jeder Handler ein undefined behandeln, das nichts bedeutet.
    expect(seen).toEqual([{ state: 'ringing' }])
  })

  it('clears the key when the server sends null', () => {
    const { channel, socket } = bridge()
    socket().receive({ key: KEY, value: { state: 'ringing' } })

    const seen: unknown[] = []
    channel.subscribe(KEY, (value) => seen.push(value))
    socket().receive({ key: KEY, value: null })

    expect(seen).toEqual([{ state: 'ringing' }, undefined])
  })

  it('yields to an island that already owns the key', () => {
    // Design §5.3: gibt es im selben Tab einen Publisher, gilt lokal. Das fällt aus der
    // Kardinalität des Kanals — die Brücke wird abgewiesen und lässt es dabei.
    const channel = createSurfaceContextChannel('acme', 'agent-desk')
    const local = channel.providePublisher({ key: KEY, publisherPluginId: 'communication' })
    local.publish({ state: 'lokal' })

    const { socket } = bridge(channel)
    socket().receive({ key: KEY, value: { state: 'vom-server' } })

    const seen: unknown[] = []
    channel.subscribe(KEY, (value) => seen.push(value))
    expect(seen).toEqual([{ state: 'lokal' }])
  })

  it('takes the key once the local island releases it', () => {
    const channel = createSurfaceContextChannel('acme', 'agent-desk')
    const local = channel.providePublisher({ key: KEY, publisherPluginId: 'communication' })
    const { socket } = bridge(channel)

    // Die erste Nachricht wird abgewiesen; danach gibt die Insel den Key frei — und ab da
    // gehört er dem Server. Die Ablehnung galt dem Moment, nicht der Lebensdauer der Seite.
    socket().receive({ key: KEY, value: { state: 'zu-früh' } })
    local.dispose()
    socket().receive({ key: KEY, value: { state: 'vom-server' } })

    const seen: unknown[] = []
    channel.subscribe(KEY, (value) => seen.push(value))
    expect(seen).toEqual([{ state: 'vom-server' }])
  })

  it('fragt nicht bei jeder Nachricht neu an, solange die Insel den Key hält', () => {
    // Die Gegenprobe zum Vorigen: Würde die Brücke die Ablehnung gar nicht merken, liefe sie
    // pro Nachricht in eine neue Anfrage — mit einer Konsolenwarnung und einem
    // Diagnose-Eintrag je Frame. Aus einem Befund würde Rauschen, das ihn zudeckt.
    const channel = createSurfaceContextChannel('acme', 'agent-desk')
    channel.providePublisher({ key: KEY, publisherPluginId: 'communication' })
    const { socket } = bridge(channel)

    socket().receive({ key: KEY, value: { state: 'eins' } })
    socket().receive({ key: KEY, value: { state: 'zwei' } })
    socket().receive({ key: KEY, value: { state: 'drei' } })

    const diagnostics = channel.diagnostics().find((entry) => entry.key === KEY)
    expect(diagnostics?.rejectedPublishers).toEqual(['callora.surface.bridge'])
  })

  it('survives a malformed frame', () => {
    const { channel, socket } = bridge()
    const seen: unknown[] = []
    channel.subscribe(KEY, (value) => seen.push(value))

    socket().receiveRaw('{nicht json')
    socket().receive({ ohne: 'key' })
    socket().receive({ key: KEY, value: 'danach' })

    expect(seen).toEqual(['danach'])
  })

  it('reconnects with growing delay after a drop', () => {
    const { scheduled, socket } = bridge()

    socket().drop()
    socket().drop()
    socket().drop()

    expect(scheduled.map((s) => s.delayMs)).toEqual([1_000, 2_000, 4_000])
  })

  it('caps the backoff so an outage does not become a thundering herd', () => {
    expect(backoffMs(1)).toBe(1_000)
    expect(backoffMs(6)).toBe(30_000)
    expect(backoffMs(50)).toBe(30_000)
  })

  it('stops reconnecting once closed', () => {
    const { instance, scheduled, socket } = bridge()

    instance.close()
    socket().drop()

    expect(scheduled).toEqual([])
    expect(socket().closed).toBe(true)
  })

  it('releases its keys on close, so an island can take them', () => {
    // Eine Brücke, die geschlossen wird und den Key weiter hält, sperrt die Insel aus,
    // die ihn bedienen könnte.
    const { instance, channel, socket } = bridge()
    socket().receive({ key: KEY, value: 'vom-server' })

    instance.close()
    const local = channel.providePublisher({ key: KEY, publisherPluginId: 'communication' })

    expect(local.accepted).toBe(true)
  })

  it('names the surface path so the server resolves the right surface', () => {
    const { socket } = bridge()

    expect(socket().url).toContain('/surface/context?path=%2Fagent-desk')
  })
})
