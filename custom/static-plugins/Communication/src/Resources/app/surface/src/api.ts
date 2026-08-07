/**
 * What a phone block may ask the server for.
 *
 * Only commands and queries. What is happening right now arrives as surface context — the
 * server publishes it and the runtime keeps the connection, so a block subscribes to a key
 * instead of polling for one.
 */

const API_BASE = '/surface-api/communication/'

/** One call as the history returns it. */
export interface CallHistoryEntry {
  callId: string
  direction: string
  remoteParty: string
  /** Our side: inbound the number reached, outbound the line it went out on. */
  localIdentity: string
  startedAt: string
  answeredAt: string | null
  endedAt: string | null
  durationSeconds: number
  outcome: string
  disconnectCause: string | null
}

/** One live call, as the active list returns it. */
export interface CallSnapshot {
  callId: string
  direction: string
  state: string
  target: string
}

/** Thrown when a call route answers with a non-OK status; carries what the server said. */
export class CallApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'CallApiError'
    this.status = status
  }
}

async function request(method: string, path: string, body?: unknown): Promise<unknown> {
  const init: RequestInit = { method, credentials: 'include' }
  if (body !== undefined) {
    init.headers = { 'content-type': 'application/json' }
    init.body = JSON.stringify(body)
  }

  const response = await fetch(`${API_BASE}${path}`, init)
  const data = await response.json().catch(() => null)
  if (!response.ok) {
    const problem = data as { error?: string } | null
    // 403 is the one worth reading aloud: it names the claim the visitor is missing, and a
    // deployment that swallows it debugs an empty panel instead.
    throw new CallApiError(response.status, problem?.error ?? `HTTP ${response.status}`)
  }

  return data
}

/** The workspace's recent calls, newest first. */
export async function listCalls(limit?: number): Promise<CallHistoryEntry[]> {
  const query = limit === undefined ? '' : `?limit=${encodeURIComponent(String(limit))}`
  const data = await request('GET', `calls${query}`)
  return Array.isArray(data) ? (data as CallHistoryEntry[]) : []
}

/** What is live right now — the starting point a reloaded panel needs. */
export async function listActiveCalls(): Promise<CallSnapshot[]> {
  const data = await request('GET', 'calls/active')
  return Array.isArray(data) ? (data as CallSnapshot[]) : []
}

/** Answers a ringing call. */
export async function acceptCall(callId: string): Promise<void> {
  await request('POST', `calls/${encodeURIComponent(callId)}/accept`)
}

/** Refuses a ringing call. */
export async function rejectCall(callId: string): Promise<void> {
  await request('POST', `calls/${encodeURIComponent(callId)}/reject`)
}

/** Ends a call. */
export async function hangupCall(callId: string): Promise<void> {
  await request('POST', `calls/${encodeURIComponent(callId)}/hangup`)
}

/** Sends keypad digits into a call. */
export async function sendDtmf(callId: string, tones: string): Promise<void> {
  await request('POST', `calls/${encodeURIComponent(callId)}/dtmf`, { tones })
}

/**
 * Dials out. Neither the line nor the quota origin is ours to choose — the server takes the
 * origin from the surface, so a page cannot dodge a limit by renaming itself.
 */
export async function placeCall(to: string, displayName?: string): Promise<CallSnapshot> {
  const body: Record<string, unknown> = { to }
  if (displayName) {
    body.displayName = displayName
  }

  return (await request('POST', 'calls', body)) as CallSnapshot
}
