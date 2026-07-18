<template>
  <section class="plugins">
    <header class="head">
      <h1>Plugins</h1>
      <div class="head-actions">
        <ExtensionSlot name="plugins.list.toolbar" />
      </div>
    </header>

    <form v-if="canInstall" class="install" @submit.prevent="install">
      <input
        v-model="installId"
        name="installId"
        class="install-input"
        placeholder="Plugin-Id aus lokalem Quellcode…"
      />
      <BaseButton type="submit" :disabled="installing || !installId.trim()">
        {{ installing ? 'Installiere…' : 'Installieren' }}
      </BaseButton>
    </form>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-if="notice" class="notice">{{ notice }}</p>
    <p v-if="loading">Lädt…</p>

    <table v-else class="grid">
      <thead>
        <tr>
          <th>Plugin</th>
          <th>Id</th>
          <th>Status</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="p in plugins" :key="p.pluginId">
          <td>{{ p.displayName }}</td>
          <td class="mono">{{ p.pluginId }}</td>
          <td>
            <span class="badge" :class="isPluginActive(p.state) ? 'badge-active' : 'badge-inactive'">
              {{ isPluginActive(p.state) ? 'Aktiv' : 'Inaktiv' }}
            </span>
          </td>
          <td class="actions">
            <button
              v-if="canExecute && isPluginActive(p.state)"
              type="button"
              class="link"
              :disabled="busyId === p.pluginId"
              @click="deactivate(p)"
            >
              Deaktivieren
            </button>
            <button
              v-else-if="canExecute"
              type="button"
              class="link"
              :disabled="busyId === p.pluginId"
              @click="activate(p)"
            >
              Aktivieren
            </button>
            <button
              v-if="canDelete"
              type="button"
              class="link-danger"
              :disabled="busyId === p.pluginId"
              @click="uninstall(p)"
            >
              Deinstallieren
            </button>
            <ExtensionSlot name="plugins.list.row-actions" :ctx="p" />
          </td>
        </tr>
        <tr v-if="!plugins.length">
          <td colspan="4" class="empty">Keine Plugins installiert.</td>
        </tr>
      </tbody>
    </table>

    <section v-if="uiLoadFailures.length || serviceConflicts.length" class="diagnostics">
      <h2>Diagnose der Admin-Erweiterungen</h2>

      <div v-if="uiLoadFailures.length" class="diag-block">
        <h3>Fehlgeschlagene Plugin-UIs</h3>
        <ul>
          <li v-for="(r, i) in uiLoadFailures" :key="i">
            <span class="mono">{{ r.pluginId }}</span> — {{ r.url }}<template v-if="r.detail">: {{ r.detail }}</template>
          </li>
        </ul>
      </div>

      <div v-if="serviceConflicts.length" class="diag-block">
        <h3>Service-Konflikte</h3>
        <ul>
          <li v-for="c in serviceConflicts" :key="c.key">
            <span class="mono">{{ c.key }}</span> — aktiv:
            <strong>{{ c.activePluginId ?? 'Host' }}</strong>, überschattet:
            {{ c.shadowedPluginIds.map((p) => p ?? 'Host').join(', ') }}
          </li>
        </ul>
      </div>
    </section>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { pluginsApi, isPluginActive, type PluginInstallation, type PluginLifecycleResult } from './pluginsApi'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import BaseButton from '@/core/ui/BaseButton.vue'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService, getServiceConflicts } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'
import { getPluginUiLoadResults } from '@/core/extensions/loader'

const plugins = ref<PluginInstallation[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)
const busyId = ref<string | null>(null)
const installId = ref('')
const installing = ref(false)

const ctx = useAuthStore().context
// Lifecycle actions map to the backend permissions: activate/deactivate need
// plugin.execute, uninstall plugin.delete, install plugin.create. UI-only gating.
const canExecute = computed(() => hasPermission(ctx.value, 'plugin.execute'))
const canDelete = computed(() => hasPermission(ctx.value, 'plugin.delete'))
const canInstall = computed(() => hasPermission(ctx.value, 'plugin.create'))

// Resolve the plugins service through the override registry: a plugin may replace it.
const api = useService('pluginsApi', pluginsApi)

// Extension diagnostics captured at bootstrap (loader load results + service
// override conflicts). Read once — this state is stable after the shell mounts.
const uiLoadFailures = getPluginUiLoadResults().filter((r) => r.status === 'failed')
const serviceConflicts = getServiceConflicts()

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    plugins.value = await api.list()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

// Shared runner for the state-changing actions: before/after hooks (a plugin may
// veto), warning surfacing, per-row busy state, and a reload afterwards.
async function lifecycleAction(
  verb: string,
  pluginId: string,
  action: () => Promise<PluginLifecycleResult>,
): Promise<void> {
  error.value = null
  notice.value = null
  const before = await runHook(`plugins.before-${verb}`, { pluginId })
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Aktion abgebrochen.'
    return
  }
  busyId.value = pluginId
  try {
    const result = await action()
    if (result.warningMessage) {
      notice.value = result.warningMessage
    }
    await runHook(`plugins.after-${verb}`, { pluginId })
    await load()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busyId.value = null
  }
}

function activate(p: PluginInstallation): Promise<void> {
  return lifecycleAction('activate', p.pluginId, () => api.activate(p.pluginId))
}

function deactivate(p: PluginInstallation): Promise<void> {
  return lifecycleAction('deactivate', p.pluginId, () => api.deactivate(p.pluginId))
}

function uninstall(p: PluginInstallation): Promise<void> {
  if (!window.confirm(`Plugin „${p.displayName}“ deinstallieren?`)) {
    return Promise.resolve()
  }
  return lifecycleAction('uninstall', p.pluginId, () => api.uninstall(p.pluginId))
}

// A before-install hook may toggle buildIfNeeded or veto; the plugin id is the
// read-only identity of what is being installed.
interface PluginInstallDraft {
  readonly pluginId: string
  buildIfNeeded: boolean
}

async function install(): Promise<void> {
  const pluginId = installId.value.trim()
  if (!pluginId) {
    return
  }
  error.value = null
  notice.value = null
  const draft: PluginInstallDraft = { pluginId, buildIfNeeded: true }
  const before = await runHook('plugins.before-install', draft)
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Installation abgebrochen.'
    return
  }
  installing.value = true
  try {
    const result = await api.installLocal(draft.pluginId, draft.buildIfNeeded)
    if (result.warningMessage) {
      notice.value = result.warningMessage
    }
    installId.value = ''
    await runHook('plugins.after-install', { pluginId })
    await load()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    installing.value = false
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.plugins {
  padding: calc(var(--cal-space) * 3);
}

.head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: calc(var(--cal-space) * 2);
}

.head-actions {
  display: flex;
  align-items: center;
  gap: var(--cal-space);
}

.install {
  display: flex;
  gap: var(--cal-space);
  margin-bottom: calc(var(--cal-space) * 2);
}

.install-input {
  flex: 1;
  max-width: 360px;
  padding: calc(var(--cal-space) * 1.25);
  border: 1px solid var(--cal-color-muted);
  border-radius: var(--cal-radius);
  background: var(--cal-color-surface);
  color: var(--cal-color-text);
  font: inherit;
}

.grid {
  width: 100%;
  border-collapse: collapse;
}

.grid th,
.grid td {
  text-align: left;
  padding: var(--cal-space);
  border-bottom: 1px solid var(--cal-color-surface);
}

.grid th {
  color: var(--cal-color-muted);
  font-weight: 600;
}

.mono {
  font-family: var(--cal-font-mono, monospace);
  color: var(--cal-color-muted);
}

.badge {
  font-size: 0.75em;
  border-radius: var(--cal-radius);
  padding: 0 calc(var(--cal-space) * 0.75);
}

.badge-active {
  color: var(--cal-color-accent);
  border: 1px solid var(--cal-color-accent);
}

.badge-inactive {
  color: var(--cal-color-muted);
  border: 1px solid var(--cal-color-muted);
}

.actions {
  display: flex;
  gap: calc(var(--cal-space) * 1.5);
  align-items: center;
}

.link {
  background: none;
  border: 0;
  color: var(--cal-color-accent);
  cursor: pointer;
  font: inherit;
  padding: 0;
}

.link-danger {
  background: none;
  border: 0;
  color: var(--cal-color-danger);
  cursor: pointer;
  font: inherit;
  padding: 0;
}

.link:disabled,
.link-danger:disabled {
  opacity: 0.5;
  cursor: default;
}

.empty {
  color: var(--cal-color-muted);
}

.error {
  color: var(--cal-color-danger);
}

.notice {
  color: var(--cal-color-accent);
}

.diagnostics {
  margin-top: calc(var(--cal-space) * 3);
  border-top: 1px solid var(--cal-color-surface);
  padding-top: calc(var(--cal-space) * 2);
}

.diagnostics h2 {
  font-size: 1.05em;
  margin-bottom: var(--cal-space);
}

.diag-block {
  margin-bottom: calc(var(--cal-space) * 1.5);
}

.diag-block h3 {
  font-size: 0.9em;
  color: var(--cal-color-danger);
  margin-bottom: 4px;
}

.diag-block ul {
  margin: 0;
  padding-left: calc(var(--cal-space) * 2);
  color: var(--cal-color-muted);
  font-size: 0.9em;
}
</style>
