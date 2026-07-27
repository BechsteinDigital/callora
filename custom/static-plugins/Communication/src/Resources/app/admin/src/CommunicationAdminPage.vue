<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'

// The plugin's operator API is reached through the host proxy; the plugin declared
// these routes via IHostAdminApiExtensionContributor. A platform operator must name
// the target workspace explicitly (?workspaceKey=…), so the page carries that field.
const API_BASE = '/api/ext/admin/plugins/communication/'
const TRANSPORTS = ['Udp', 'Tcp', 'Tls'] as const

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
}

const workspaceKey = ref(new URLSearchParams(window.location.search).get('workspaceKey') ?? '')
const accounts = ref<SipAccount[]>([])
const loading = ref(false)
const busy = ref(false)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)
const form = reactive({ displayName: '', host: '', port: 5060, transport: 'Udp', username: '', password: '' })

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
  if (!form.displayName.trim() || !form.host.trim() || !form.username.trim() || !form.password) {
    error.value = 'Anzeigename, Host, Benutzername und Passwort sind erforderlich.'
    return
  }
  await run(
    request('POST', 'sip-accounts', {
      displayName: form.displayName.trim(),
      host: form.host.trim(),
      port: Number(form.port) || 5060,
      transport: form.transport,
      authMethod: 'Digest',
      username: form.username.trim(),
      password: form.password,
      enabled: true,
    }),
    'Account angelegt.',
  )
  Object.assign(form, { displayName: '', host: '', port: 5060, transport: 'Udp', username: '', password: '' })
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
          Benutzername<input v-model="form.username" />
        </label>
        <label style="display: flex; flex-direction: column; font-size: 0.85rem; gap: 0.15rem">
          Passwort<input v-model="form.password" type="password" />
        </label>
        <button type="submit" :disabled="busy">Account anlegen</button>
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
            <td style="padding: 0.4rem">{{ a.status }}<span v-if="a.lastError"> ({{ a.lastError }})</span></td>
            <td style="padding: 0.4rem">{{ a.enabled ? 'ja' : 'nein' }}</td>
            <td style="padding: 0.4rem; display: flex; gap: 0.4rem">
              <button :disabled="busy" @click="toggle(a)">{{ a.enabled ? 'Deaktivieren' : 'Aktivieren' }}</button>
              <button :disabled="busy" @click="remove(a)">Löschen</button>
            </td>
          </tr>
        </tbody>
      </table>
    </template>
  </section>
</template>
