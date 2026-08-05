<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'

// The workspace dialer (#116). It drives the same call-control routes an integration or an MCP
// agent would call, so what an operator can do here is exactly what the API allows — no separate
// control path, no separate state machine.
const API_BASE = '/api/ext/admin/plugins/communication/'

const props = defineProps<{ workspaceKey: string }>()

interface Call {
  callId: string
  direction: string
  state: string
  target: string
}

interface CallEvent {
  eventName: string
  callId: string
  direction: string
  state: string
  remoteParty: string
  occurredAt: string
}

const STATE_LABELS: Record<string, string> = {
  Connecting: 'Verbindet',
  Ringing: 'Klingelt',
  Connected: 'Verbunden',
  Terminated: 'Beendet',
}

const DTMF_KEYS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '*', '0', '#'] as const

const calls = ref<Call[]>([])
const events = ref<CallEvent[]>([])
const target = ref('')
const selectedCallId = ref<string | null>(null)
const busy = ref(false)
const error = ref<string | null>(null)
const streamState = ref<'offline' | 'live'>('offline')

// The stream is best effort, so the list — not the socket — is the source of truth. Every event
// triggers a refresh rather than being applied as a delta: a dropped event then costs one stale
// frame instead of a permanently wrong row.
let socket: WebSocket | null = null

const selectedCall = computed(() => calls.value.find((call) => call.callId === selectedCallId.value) ?? null)
const canAnswer = computed(() => selectedCall.value?.direction === 'Inbound' && selectedCall.value?.state === 'Ringing')
const canSendDtmf = computed(() => selectedCall.value?.state === 'Connected')

function stateLabel(call: Call): string {
  return STATE_LABELS[call.state] ?? call.state
}

async function request(method: string, path: string, body?: unknown): Promise<unknown> {
  const ws = encodeURIComponent(props.workspaceKey.trim())
  const sep = path.includes('?') ? '&' : '?'
  const init: RequestInit = { method, credentials: 'include' }
  if (body !== undefined) {
    init.headers = { 'content-type': 'application/json' }
    init.body = JSON.stringify(body)
  }
  const res = await fetch(`${API_BASE}${path}${sep}workspaceKey=${ws}`, init)
  if (res.status === 204) {
    return null
  }
  const data = await res.json().catch(() => null)
  if (!res.ok) {
    const problem = data as { detail?: string; title?: string; error?: string } | null
    throw new Error(problem?.detail ?? problem?.title ?? problem?.error ?? `HTTP ${res.status}`)
  }
  return data
}

async function reload(): Promise<void> {
  try {
    calls.value = ((await request('GET', 'calls/active')) as Call[]) ?? []
    if (selectedCallId.value && !calls.value.some((call) => call.callId === selectedCallId.value)) {
      selectedCallId.value = null
    }
    if (!selectedCallId.value && calls.value.length > 0) {
      selectedCallId.value = calls.value[0].callId
    }
  } catch (err) {
    error.value = (err as Error).message
  }
}

async function run(action: Promise<unknown>): Promise<void> {
  busy.value = true
  error.value = null
  try {
    await action
    await reload()
  } catch (err) {
    error.value = (err as Error).message
  } finally {
    busy.value = false
  }
}

function place(): void {
  if (!target.value.trim()) {
    error.value = 'Bitte eine Zielrufnummer angeben.'
    return
  }
  void run(request('POST', 'calls', { to: target.value.trim() }).then(() => { target.value = '' }))
}

function control(callId: string, verb: 'accept' | 'reject' | 'hangup'): void {
  void run(request('POST', `calls/${encodeURIComponent(callId)}/${verb}`))
}

function sendDtmf(tone: string): void {
  if (!selectedCallId.value) {
    return
  }
  void run(request('POST', `calls/${encodeURIComponent(selectedCallId.value)}/dtmf`, { tones: tone }))
}

async function connectStream(): Promise<void> {
  disconnectStream()
  if (!props.workspaceKey.trim()) {
    return
  }

  try {
    // The handshake cannot carry an Authorization header, so the permission check happens on this
    // request and the socket carries only its short-lived, single-use ticket.
    const ticket = (await request('POST', 'calls/event-stream')) as { connectPath: string }
    const url = new URL(ticket.connectPath, window.location.href)
    url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:'

    const opened = new WebSocket(url.toString())
    socket = opened
    opened.onopen = () => { streamState.value = 'live' }
    opened.onclose = () => {
      streamState.value = 'offline'
      if (socket === opened) {
        socket = null
      }
    }
    opened.onmessage = (message) => {
      const event = JSON.parse(message.data as string) as CallEvent
      events.value = [event, ...events.value].slice(0, 20)
      void reload()
    }
  } catch (err) {
    error.value = (err as Error).message
  }
}

function disconnectStream(): void {
  socket?.close()
  socket = null
  streamState.value = 'offline'
}

watch(
  () => props.workspaceKey,
  (key) => {
    events.value = []
    calls.value = []
    selectedCallId.value = null
    if (key.trim()) {
      void reload()
      void connectStream()
    } else {
      disconnectStream()
    }
  },
  { immediate: true },
)

onBeforeUnmount(disconnectStream)
</script>

<template>
  <section style="margin-top: 2rem">
    <h2 style="display: flex; align-items: baseline; gap: 0.75rem">
      Dialer
      <small :style="{ color: streamState === 'live' ? 'var(--cal-color-success, #2e7d32)' : 'var(--cal-color-text-muted, #777)' }">
        {{ streamState === 'live' ? 'Live' : 'Kein Live-Stream' }}
      </small>
    </h2>

    <p v-if="error" style="color: var(--cal-color-danger, #c0392b)">{{ error }}</p>

    <form style="display: flex; gap: 0.5rem; margin: 0.75rem 0 1.25rem" @submit.prevent="place">
      <input v-model="target" placeholder="+49301234567" style="flex: 1" />
      <button type="submit" :disabled="busy">Anrufen</button>
      <button type="button" :disabled="busy" @click="reload">Aktualisieren</button>
    </form>

    <h3>Aktive Gespräche</h3>
    <p v-if="calls.length === 0" style="color: var(--cal-color-text-muted, #777)">Derzeit keine Gespräche.</p>
    <table v-else style="width: 100%; border-collapse: collapse">
      <thead>
        <tr>
          <th style="text-align: left">Gegenstelle</th>
          <th style="text-align: left">Richtung</th>
          <th style="text-align: left">Status</th>
          <th style="text-align: left">Aktionen</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="call in calls"
          :key="call.callId"
          :style="{ background: call.callId === selectedCallId ? 'var(--cal-color-surface-selected, #eef3ff)' : 'transparent' }"
          @click="selectedCallId = call.callId"
        >
          <td>{{ call.target }}</td>
          <td>{{ call.direction === 'Inbound' ? 'Eingehend' : 'Ausgehend' }}</td>
          <td>{{ stateLabel(call) }}</td>
          <td style="display: flex; gap: 0.35rem">
            <button
              v-if="call.direction === 'Inbound' && call.state === 'Ringing'"
              :disabled="busy"
              @click.stop="control(call.callId, 'accept')"
            >
              Annehmen
            </button>
            <button
              v-if="call.direction === 'Inbound' && call.state === 'Ringing'"
              :disabled="busy"
              @click.stop="control(call.callId, 'reject')"
            >
              Ablehnen
            </button>
            <button :disabled="busy" @click.stop="control(call.callId, 'hangup')">Auflegen</button>
          </td>
        </tr>
      </tbody>
    </table>

    <template v-if="selectedCall">
      <h3 style="margin-top: 1.5rem">Tastenfeld — {{ selectedCall.target }}</h3>
      <p v-if="!canSendDtmf" style="color: var(--cal-color-text-muted, #777)">
        Töne lassen sich erst im verbundenen Gespräch senden.
        <template v-if="canAnswer">Das Gespräch klingelt noch.</template>
      </p>
      <div v-else style="display: grid; grid-template-columns: repeat(3, 3rem); gap: 0.35rem">
        <button v-for="key in DTMF_KEYS" :key="key" :disabled="busy" @click="sendDtmf(key)">{{ key }}</button>
      </div>
    </template>

    <h3 style="margin-top: 1.5rem">Letzte Ereignisse</h3>
    <p v-if="events.length === 0" style="color: var(--cal-color-text-muted, #777)">Noch keine Ereignisse empfangen.</p>
    <ul v-else style="list-style: none; padding: 0; font-family: monospace; font-size: 0.85rem">
      <li v-for="(event, index) in events" :key="`${event.callId}-${event.occurredAt}-${index}`">
        {{ new Date(event.occurredAt).toLocaleTimeString() }} — {{ event.eventName }} — {{ event.remoteParty }}
      </li>
    </ul>
  </section>
</template>
