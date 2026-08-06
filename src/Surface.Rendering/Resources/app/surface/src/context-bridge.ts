import type { SurfaceContextChannel, SurfaceContextPublisher } from './surface-context-channel'

/**
 * The client half of the realtime bridge: one socket per open surface, feeding
 * server-published values into the local context channel.
 *
 * A view never learns which half a value came from. It subscribes to a key; whether an
 * island in the same tab published it or the server did is the resolver's business —
 * which is what makes the topology a customer's configuration decision rather than
 * something block code has to know (design §5.3).
 *
 * The precedence falls out of the channel rather than being decided here: the bridge asks
 * for a publisher like anyone else, and a `single`-cardinality key already claimed by a
 * local island refuses it. Local wins, without a rule that says so.
 */

/** How long to wait before reconnecting, growing per attempt. */
const RECONNECT_BASE_MS = 1_000
const RECONNECT_MAX_MS = 30_000

/** Who publishes bridged values, for the channel's diagnostics. */
const BRIDGE_PUBLISHER_ID = 'callora.surface.bridge'

export interface SurfaceContextBridge {
  /** Closes the socket and releases every key the bridge claimed. */
  close(): void
}

export interface SurfaceContextBridgeOptions {
  /** Path the surface is rendered at; the server resolves the surface from it. */
  path?: string
  /** Injected for tests. Defaults to the platform WebSocket. */
  createSocket?: (url: string) => WebSocket
  /** Injected for tests. Defaults to setTimeout. */
  schedule?: (handler: () => void, delayMs: number) => void
}

interface BridgeMessage {
  key: string
  value: unknown
}

/**
 * Opens the bridge. Returns immediately — the socket connects in the background, and a
 * surface renders and stays usable whether or not it ever does.
 */
export function connectSurfaceContextBridge(
  channel: SurfaceContextChannel,
  options: SurfaceContextBridgeOptions = {},
): SurfaceContextBridge {
  const createSocket = options.createSocket ?? ((url: string) => new WebSocket(url))
  const schedule = options.schedule ?? ((handler, delayMs) => window.setTimeout(handler, delayMs))
  const publishers = new Map<string, SurfaceContextPublisher>()

  let socket: WebSocket | undefined
  let attempt = 0
  let closed = false

  const publish = (message: BridgeMessage): void => {
    let publisher = publishers.get(message.key)
    if (!publisher) {
      publisher = channel.providePublisher({
        key: message.key,
        publisherPluginId: BRIDGE_PUBLISHER_ID,
      })
      publishers.set(message.key, publisher)
    }

    // Refused means an island in this tab already owns the key. Dropping the value is
    // correct: the local publisher is closer to the user and has the newer state.
    if (publisher.accepted) {
      if (message.value === null || message.value === undefined) {
        publisher.clear()
      } else {
        publisher.publish(message.value)
      }
    }
  }

  const connect = (): void => {
    if (closed) {
      return
    }

    socket = createSocket(bridgeUrl(options.path))

    socket.onopen = () => {
      attempt = 0
    }

    socket.onmessage = (event: MessageEvent) => {
      const message = parse(event.data)
      if (message) {
        publish(message)
      }
    }

    // Both paths lead here; a browser fires error before close on a failed connect, and
    // reconnecting from both would double the attempts.
    socket.onclose = () => {
      if (closed) {
        return
      }

      attempt += 1
      schedule(connect, backoffMs(attempt))
    }
  }

  connect()

  return {
    close(): void {
      closed = true
      socket?.close()
      // Releasing the keys matters: a bridge that closed while still holding a
      // single-cardinality key would lock out the island that could serve it.
      publishers.forEach((publisher) => publisher.dispose())
      publishers.clear()
    },
  }
}

/**
 * Exponential backoff, capped. A surface that reconnects every second through an outage
 * turns one server's bad minute into a thundering herd.
 */
export function backoffMs(attempt: number): number {
  return Math.min(RECONNECT_BASE_MS * 2 ** (attempt - 1), RECONNECT_MAX_MS)
}

/** Same origin, protocol matched to the page — an https page cannot open a ws:// socket. */
function bridgeUrl(path?: string): string {
  const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:'
  const at = path ?? window.location.pathname
  return `${protocol}//${window.location.host}/surface/context?path=${encodeURIComponent(at)}`
}

function parse(data: unknown): BridgeMessage | undefined {
  if (typeof data !== 'string') {
    return undefined
  }

  try {
    const parsed: unknown = JSON.parse(data)
    if (
      parsed &&
      typeof parsed === 'object' &&
      typeof (parsed as BridgeMessage).key === 'string' &&
      (parsed as BridgeMessage).key.length > 0
    ) {
      return parsed as BridgeMessage
    }
  } catch {
    // A malformed frame is dropped. The bridge carries UI state, so the cost is a stale
    // panel until the next value — not a reason to tear down a working connection.
  }

  return undefined
}
