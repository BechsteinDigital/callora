<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import CallDialer from './CallDialer.vue'

// The plugin's operator API is reached through the host proxy; the plugin declared
// these routes via IHostAdminApiExtensionContributor. A platform operator must name
// the target workspace explicitly (?workspaceKey=…), so the page carries that field.
const API_BASE = '/api/ext/admin/plugins/communication/'
const TRANSPORTS = ['Udp', 'Tcp', 'Tls'] as const
// Matches SipAuthMethodSupport.Supported on the backend. The mode (Register/Trunk) and
// registration expiry are derived server-side from the method, so the form only picks the
// method and its credential shape.
//
// Only digest is offered: the voice provider cannot connect IP-authenticated trunks
// (callora-voip-sdk#104, no registration-less mode) or mutual TLS (callora-voip-sdk#183,
// TLS config is client-wide and file-based). The API refuses both with 422, so offering
// them here would only produce accounts that never come up. Re-add an entry when the
// backend adds the method to SipAuthMethodSupport.
const AUTH_METHODS = [
  { value: 'Digest', label: 'Digest (Registrierung)' },
] as const

/** One step of what happened to a call, as its participants recorded it. */
interface CallJourneyStep {
  source: string
  step: string
  detail: string | null
  at: string
}

/** A call as the history returns it. */
interface CallHistoryEntry {
  callId: string
  direction: string
  remoteParty: string
  /** Unsere Seite: eingehend die erreichte Nummer, ausgehend die Leitung. */
  localIdentity: string
  startedAt: string
  answeredAt: string | null
  endedAt: string | null
  durationSeconds: number
  outcome: string
  disconnectCause: string | null
  journey: CallJourneyStep[]
}

/** Eine Nummer des Workspaces mit allem, was ein Betreiber dazu fragt. */
interface NumberPlanEntry {
  number: string
  channelId: string
  channelDisplayName: string
  /** Wie viele Leitungen der Leitung diese Nummer halten darf; null heißt unbegrenzt. */
  maxConcurrentCalls: number | null
  recentCalls: number
  lastCallAt: string | null
}

interface SipAccount {
  id: string
  displayName: string
  host: string
  port: number
  transport: string
  mode: string
  status: string
  enabled: boolean
  lastError: string | null
  lastStatusChangeAt: string | null
  lastRegisteredAt: string | null
}

// SipAccountStatus on the backend. The distinction matters operationally: Connecting is
// still coming up, Degraded still carries calls, Failed does not, and Disabled is a
// deliberate choice rather than a fault (#112).
const STATUS_LABELS: Record<string, { label: string; color: string }> = {
  Disabled: { label: 'Deaktiviert', color: 'var(--cal-color-text-muted, #777)' },
  Connecting: { label: 'Verbindet', color: 'var(--cal-color-warning, #b26a00)' },
  Up: { label: 'Registriert', color: 'var(--cal-color-success, #2e7d32)' },
  Degraded: { label: 'Eingeschränkt', color: 'var(--cal-color-warning, #b26a00)' },
  Failed: { label: 'Fehlgeschlagen', color: 'var(--cal-color-danger, #c0392b)' },
}

function statusOf(account: SipAccount): { label: string; color: string } {
  return STATUS_LABELS[account.status] ?? { label: account.status, color: 'inherit' }
}

function formatMoment(value: string | null): string {
  return value ? new Date(value).toLocaleString() : '-'
}

function formatTime(value: string): string {
  const at = new Date(value)
  return Number.isNaN(at.getTime()) ? value : at.toLocaleTimeString()
}

/** Outcomes an operator reads differently: a call nobody answered is not a failure of the line. */
const OUTCOME_LABELS: Record<string, string> = {
  InProgress: 'läuft',
  Completed: 'beendet',
  Missed: 'nicht angenommen',
  Rejected: 'abgewiesen',
  Failed: 'fehlgeschlagen',
  Interrupted: 'abgebrochen',
}

function outcomeOf(call: CallHistoryEntry): string {
  return OUTCOME_LABELS[call.outcome] ?? call.outcome
}

function toggleJourney(call: CallHistoryEntry): void {
  openCall.value = openCall.value === call.callId ? null : call.callId
}

function startEditingQuota(entry: NumberPlanEntry): void {
  editingNumber.value = entry.number
  quotaDraft.value = entry.maxConcurrentCalls === null ? '' : String(entry.maxConcurrentCalls)
}

function saveQuota(entry: NumberPlanEntry): void {
  const raw = quotaDraft.value.trim()
  // Leeres Feld heißt „kein Limit" — dieselbe Regel wie im Backend: keine Konfiguration ist
  // unbegrenzt und nicht null.
  const limit = raw === '' ? null : Number(raw)
  if (limit !== null && (!Number.isInteger(limit) || limit < 1)) {
    error.value = 'Das Kontingent muss mindestens 1 sein — oder leer für unbegrenzt.'
    return
  }
  void run(
    request('POST', 'numbers/quota', {
      channelId: entry.channelId,
      number: entry.number,
      maxConcurrentCalls: limit,
    }).then(() => {
      editingNumber.value = null
    }),
    limit === null
      ? `Kontingent für ${entry.number} entfernt.`
      : `${entry.number}: höchstens ${limit} gleichzeitige Anrufe.`,
  )
}

const workspaceKey = ref(new URLSearchParams(window.location.search).get('workspaceKey') ?? '')
const accounts = ref<SipAccount[]>([])
const calls = ref<CallHistoryEntry[]>([])
const numbers = ref<NumberPlanEntry[]>([])
// Nur eine Zeile im Bearbeiten-Modus: Ein Kontingent wird selten und einzeln geändert, und ein
// Formular pro Zeile wäre eine Tabelle voller Eingabefelder ohne Zustand.
const editingNumber = ref<string | null>(null)
const quotaDraft = ref<string>('')
// One call open at a time: the interesting thing is one call's sequence, and several expanded at
// once turns a list into a wall.
const openCall = ref<string | null>(null)
const loading = ref(false)
const busy = ref(false)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)
const form = reactive({
  displayName: '',
  host: '',
  port: 5060,
  transport: 'Udp',
  authMethod: 'Digest',
  username: '',
  password: '',
  authId: '',
})

function resetForm(): void {
  Object.assign(form, {
    displayName: '', host: '', port: 5060, transport: 'Udp',
    authMethod: 'Digest', username: '', password: '', authId: '',
  })
}

async function request(method: string, path: string, body?: unknown): Promise<unknown> {
  const ws = encodeURIComponent(workspaceKey.value.trim())
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
  if (!workspaceKey.value.trim()) {
    error.value = 'Bitte einen Workspace angeben.'
    return
  }
  loading.value = true
  error.value = null
  try {
    accounts.value = ((await request('GET', 'sip-accounts')) as SipAccount[]) ?? []

    try {
      numbers.value = ((await request('GET', 'numbers')) as NumberPlanEntry[]) ?? []
    } catch {
      numbers.value = []
    }

    // Loaded after the accounts and allowed to fail on its own: an operator whose history is
    // unavailable should still be able to fix the account that is probably the reason.
    try {
      calls.value = ((await request('GET', 'calls?limit=25')) as CallHistoryEntry[]) ?? []
    } catch {
      calls.value = []
    }
  } catch (err) {
    error.value = (err as Error).message
    accounts.value = []
  } finally {
    loading.value = false
  }
}

async function run(action: Promise<unknown>, success: string): Promise<void> {
  busy.value = true
  error.value = null
  notice.value = null
  try {
    await action
    notice.value = success
    await reload()
  } catch (err) {
    error.value = (err as Error).message
  } finally {
    busy.value = false
  }
}

function toggle(account: SipAccount): void {
  const verb = account.enabled ? 'disable' : 'enable'
  void run(
    request('POST', `sip-accounts/${encodeURIComponent(account.id)}/${verb}`),
    `Account ${account.displayName} ${account.enabled ? 'deaktiviert' : 'aktiviert'}.`,
  )
}

function remove(account: SipAccount): void {
  void run(request('DELETE', `sip-accounts/${encodeURIComponent(account.id)}`), `Account ${account.displayName} gelöscht.`)
}

async function create(): Promise<void> {
  if (!form.displayName.trim() || !form.host.trim()) {
    error.value = 'Anzeigename und Host sind erforderlich.'
    return
  }

  const body: Record<string, unknown> = {
    displayName: form.displayName.trim(),
    host: form.host.trim(),
    port: Number(form.port) || 5060,
    transport: form.transport,
    authMethod: form.authMethod,
    enabled: true,
  }

  // Credential shape per method (mode/expiry are derived server-side). Only digest is
  // offered; the backend refuses the other methods with 422 until the provider supports them.
  if (form.authMethod === 'Digest') {
    if (!form.username.trim() || !form.password) {
      error.value = 'Für Digest sind Benutzername und Passwort erforderlich.'
      return
    }
    body.username = form.username.trim()
    body.password = form.password
    if (form.authId.trim()) {
      body.authId = form.authId.trim()
    }
  }

  await run(request('POST', 'sip-accounts', body), 'Account angelegt.')
  resetForm()
}

onMounted(() => {
  if (workspaceKey.value.trim()) {
    void reload()
  }
})
</script>

<template>
  <section style="padding: calc(var(--cal-space, 8px) * 2); max-width: 60rem">
    <h1>Communication — SIP-Accounts</h1>

    <div style="display: flex; gap: 0.5rem; align-items: center; margin: 0.5rem 0 1rem">
      <label for="cal-comm-ws">Workspace:</label>
      <input id="cal-comm-ws" v-model="workspaceKey" placeholder="workspace-key" />
      <button :disabled="loading" @click="reload">{{ loading ? 'Lädt…' : 'Laden' }}</button>
    </div>

    <p v-if="error" style="color: var(--cal-color-danger, #c0392b)">{{ error }}</p>
    <p v-if="notice" style="color: var(--cal-color-success, #2e7d32)">{{ notice }}</p>

    <template v-if="workspaceKey.trim()">
      <form
        style="display: grid; grid-template-columns: repeat(3, 1fr); gap: 0.75rem; align-items: end; margin-bottom: 1.5rem"
        @submit.prevent="create"
      >
        <label style="display: flex; flex-direction: column; font-size: 0.85rem; gap: 0.15rem">
          Anzeigename<input v-model="form.displayName" />
        </label>
        <label style="display: flex; flex-direction: column; font-size: 0.85rem; gap: 0.15rem">
          Host<input v-model="form.host" placeholder="sip.example.com" />
        </label>
        <label style="display: flex; flex-direction: column; font-size: 0.85rem; gap: 0.15rem">
          Port<input v-model="form.port" type="number" />
        </label>
        <label style="display: flex; flex-direction: column; font-size: 0.85rem; gap: 0.15rem">
          Transport
          <select v-model="form.transport">
            <option v-for="t in TRANSPORTS" :key="t" :value="t">{{ t }}</option>
          </select>
        </label>
        <label style="display: flex; flex-direction: column; font-size: 0.85rem; gap: 0.15rem">
          Verfahren
          <select v-model="form.authMethod">
            <option v-for="m in AUTH_METHODS" :key="m.value" :value="m.value">{{ m.label }}</option>
          </select>
        </label>

        <template v-if="form.authMethod === 'Digest'">
          <label style="display: flex; flex-direction: column; font-size: 0.85rem; gap: 0.15rem">
            Benutzername<input v-model="form.username" />
          </label>
          <label style="display: flex; flex-direction: column; font-size: 0.85rem; gap: 0.15rem">
            Passwort<input v-model="form.password" type="password" />
          </label>
          <label style="display: flex; flex-direction: column; font-size: 0.85rem; gap: 0.15rem">
            Auth-ID (optional)<input v-model="form.authId" />
          </label>
        </template>

        <button type="submit" :disabled="busy" style="grid-column: 1 / -1; justify-self: start">Account anlegen</button>
      </form>

      <p v-if="!accounts.length">{{ loading ? 'Lädt…' : 'Keine SIP-Accounts in diesem Workspace.' }}</p>
      <table v-else style="width: 100%; border-collapse: collapse">
        <thead>
          <tr>
            <th v-for="c in ['Name', 'Host', 'Modus', 'Status', 'Aktiv', '']" :key="c" style="text-align: left; padding: 0.4rem; border-bottom: 1px solid var(--cal-color-surface, #ddd)">
              {{ c }}
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="a in accounts" :key="a.id">
            <td style="padding: 0.4rem">{{ a.displayName }}</td>
            <td style="padding: 0.4rem">{{ a.host }}:{{ a.port }} {{ a.transport }}</td>
            <td style="padding: 0.4rem">{{ a.mode }}</td>
            <td style="padding: 0.4rem">
              <span :style="{ color: statusOf(a).color }">{{ statusOf(a).label }}</span>
              <span v-if="a.lastError" style="display: block; font-size: 0.8rem; color: var(--cal-color-danger, #c0392b)">
                {{ a.lastError }}
              </span>
              <span style="display: block; font-size: 0.75rem; color: var(--cal-color-text-muted, #777)">
                zuletzt registriert: {{ formatMoment(a.lastRegisteredAt) }}
              </span>
            </td>
            <td style="padding: 0.4rem">{{ a.enabled ? 'ja' : 'nein' }}</td>
            <td style="padding: 0.4rem; display: flex; gap: 0.4rem">
              <button :disabled="busy" @click="toggle(a)">{{ a.enabled ? 'Deaktivieren' : 'Aktivieren' }}</button>
              <button :disabled="busy" @click="remove(a)">Löschen</button>
            </td>
          </tr>
        </tbody>
      </table>

      <h2 style="margin-top: 2rem">Rufnummern</h2>
      <p style="font-size: 0.85rem; color: var(--cal-color-text-muted, #777); margin-top: 0">
        Was dieser Workspace erreichen kann: welche Leitung eine Nummer liefert, wie viele ihrer
        gleichzeitigen Anrufe die Nummer halten darf, und was zuletzt darauf ankam.
      </p>

      <p v-if="!numbers.length">
        {{ loading ? 'Lädt…' : 'Keine Leitung meldet eigene Nummern. Ein IP-Trunk ohne DID-Liste nimmt jede Nummer und kann keine nennen.' }}
      </p>
      <table v-else style="width: 100%; border-collapse: collapse">
        <thead>
          <tr>
            <th v-for="c in ['Nummer', 'Leitung', 'Gleichzeitig', 'Zuletzt', '']" :key="c" style="text-align: left; padding: 0.4rem; border-bottom: 1px solid var(--cal-color-surface, #ddd)">
              {{ c }}
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="entry in numbers" :key="`${entry.channelId}:${entry.number}`">
            <td style="padding: 0.4rem">{{ entry.number }}</td>
            <td style="padding: 0.4rem">{{ entry.channelDisplayName }}</td>
            <td style="padding: 0.4rem">
              <template v-if="editingNumber === entry.number">
                <input
                  v-model="quotaDraft"
                  type="number"
                  min="1"
                  placeholder="unbegrenzt"
                  style="width: 7rem"
                  @keyup.enter="saveQuota(entry)"
                />
              </template>
              <template v-else>
                {{ entry.maxConcurrentCalls === null ? 'unbegrenzt' : entry.maxConcurrentCalls }}
              </template>
            </td>
            <td style="padding: 0.4rem">
              <span v-if="entry.lastCallAt">
                {{ formatMoment(entry.lastCallAt) }}
                <span style="display: block; font-size: 0.75rem; color: var(--cal-color-text-muted, #777)">
                  {{ entry.recentCalls }} von den letzten Anrufen
                </span>
              </span>
              <span v-else style="color: var(--cal-color-text-muted, #777)">nichts angekommen</span>
            </td>
            <td style="padding: 0.4rem; display: flex; gap: 0.4rem">
              <template v-if="editingNumber === entry.number">
                <button :disabled="busy" @click="saveQuota(entry)">Speichern</button>
                <button :disabled="busy" @click="editingNumber = null">Abbrechen</button>
              </template>
              <button v-else :disabled="busy" @click="startEditingQuota(entry)">Kontingent</button>
            </td>
          </tr>
        </tbody>
      </table>

      <CallDialer :workspace-key="workspaceKey" />

      <h2 style="margin-top: 2rem">Letzte Anrufe</h2>
      <p style="font-size: 0.85rem; color: var(--cal-color-text-muted, #777); margin-top: 0">
        Ein Anruf zeigt auf Klick, was mit ihm passiert ist — welche Nummer er erreicht hat, wer ihn
        übernommen hat und woran es lag, wenn er nirgends ankam.
      </p>

      <p v-if="!calls.length">{{ loading ? 'Lädt…' : 'Noch keine Anrufe in diesem Workspace.' }}</p>
      <table v-else style="width: 100%; border-collapse: collapse">
        <thead>
          <tr>
            <th v-for="c in ['Zeit', 'Richtung', 'Gegenstelle', 'Erreicht', 'Ergebnis', 'Dauer', '']" :key="c" style="text-align: left; padding: 0.4rem; border-bottom: 1px solid var(--cal-color-surface, #ddd)">
              {{ c }}
            </th>
          </tr>
        </thead>
        <tbody>
          <template v-for="call in calls" :key="call.callId">
            <tr>
              <td style="padding: 0.4rem">{{ formatMoment(call.startedAt) }}</td>
              <td style="padding: 0.4rem">{{ call.direction === 'Inbound' ? 'eingehend' : 'ausgehend' }}</td>
              <td style="padding: 0.4rem">{{ call.remoteParty }}</td>
              <td style="padding: 0.4rem">{{ call.localIdentity }}</td>
              <td style="padding: 0.4rem">
                {{ outcomeOf(call) }}
                <span v-if="call.disconnectCause" style="display: block; font-size: 0.75rem; color: var(--cal-color-text-muted, #777)">
                  {{ call.disconnectCause }}
                </span>
              </td>
              <td style="padding: 0.4rem">{{ call.durationSeconds }}s</td>
              <td style="padding: 0.4rem">
                <button v-if="call.journey.length" @click="toggleJourney(call)">
                  {{ openCall === call.callId ? 'Verlauf schließen' : `Verlauf (${call.journey.length})` }}
                </button>
                <span v-else style="font-size: 0.75rem; color: var(--cal-color-text-muted, #777)">
                  nichts aufgezeichnet
                </span>
              </td>
            </tr>
            <tr v-if="openCall === call.callId">
              <td colspan="7" style="padding: 0 0.4rem 0.8rem">
                <ol style="margin: 0; padding-left: 1.2rem">
                  <li v-for="(step, index) in call.journey" :key="index" style="font-size: 0.85rem; line-height: 1.6">
                    <span style="font-variant-numeric: tabular-nums; color: var(--cal-color-text-muted, #777)">
                      {{ formatTime(step.at) }}
                    </span>
                    <code style="margin: 0 0.4rem">{{ step.step }}</code>
                    <span v-if="step.detail">{{ step.detail }}</span>
                    <span style="font-size: 0.75rem; color: var(--cal-color-text-muted, #777)"> — {{ step.source }}</span>
                  </li>
                </ol>
              </td>
            </tr>
          </template>
        </tbody>
      </table>
    </template>
  </section>
</template>
