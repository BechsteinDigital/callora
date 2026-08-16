<template>
  <CalPage>
    <CalPageHeader title="Plugins" description="Installierte Erweiterungen, ihr Zustand und ihre Signatur.">
      <template #actions>
        <ExtensionSlot name="plugins.list.toolbar" />
      </template>
    </CalPageHeader>

    <CalCard v-if="canInstall" class="plugins__install" title="Plugin installieren">
      <form class="plugins__form" @submit.prevent="install">
        <CalField v-slot="{ id }" label="Plugin-Id" description="Aus dem lokalen Quellverzeichnis.">
          <CalInput :id="id" v-model="installId" name="installId" placeholder="communication" :icon="Package" />
        </CalField>
        <CalButton
          type="submit"
          variant="primary"
          :icon="Download"
          :loading="installing"
          :disabled="!installId.trim()"
        >
          Installieren
        </CalButton>
      </form>
    </CalCard>

    <CalAlert v-if="notice" class="plugins__notice" tone="warning" dismissible @dismiss="notice = null">
      {{ notice }}
    </CalAlert>

    <CalCard flush>
      <CalDataTable
        :columns="columns"
        :rows="plugins"
        row-key="pluginId"
        :loading="loading"
        :error="error"
        :empty-icon="Puzzle"
        empty-title="Keine Plugins installiert."
        empty-description="Erweiterungen bringen zusätzliche Funktionen — etwa Telefonie oder Videokonferenz."
      >
        <template #cell-state="{ row }">
          <!--
            Vier Zustände, nicht zwei: Datei fehlt · aktiv UND läuft · aktiv, läuft aber NICHT ·
            inaktiv. Die beiden mittleren sind die, die niemand sah — ein Plugin, dessen
            Aktivierung beim Start scheiterte, stand als „Aktiv" in der Liste, und der Grund nur
            in einer Logzeile. Beim fehlenden Assembly war es dasselbe: Die Liste sagte
            „installiert", sichtbar wurde es als fehlende Oberfläche.
          -->
          <CalBadge :tone="toneOf(row)" dot :title="stateHintOf(row)">
            {{ labelOf(row) }}
          </CalBadge>
        </template>

        <template #cell-signature="{ row }">
          <CalBadge
            v-if="signatureStates[row.pluginId]"
            :tone="signatureTone(signatureStates[row.pluginId])"
            variant="outline"
            :title="signatureStates[row.pluginId]"
          >
            {{ signatureLabel(signatureStates[row.pluginId]) }}
          </CalBadge>
          <span v-else>—</span>
        </template>

        <template #cell-actions="{ row }">
          <div class="plugins__actions">
            <CalButton
              v-if="canExecute"
              variant="ghost"
              size="sm"
              :disabled="busyId === row.pluginId"
              @click="isPluginActive(row.state) ? deactivate(row) : activate(row)"
            >
              {{ isPluginActive(row.state) ? 'Deaktivieren' : 'Aktivieren' }}
            </CalButton>
            <CalButton
              v-if="canDelete"
              variant="danger-ghost"
              size="sm"
              :disabled="busyId === row.pluginId"
              @click="uninstall(row)"
            >
              Deinstallieren
            </CalButton>
            <ExtensionSlot name="plugins.list.row-actions" :ctx="row" />
          </div>
        </template>
      </CalDataTable>
    </CalCard>

    <CalCard
      v-if="uiLoadFailures.length || serviceConflicts.length"
      class="plugins__diagnostics"
      title="Diagnose der Admin-Erweiterungen"
      description="Beim Start dieser Oberfläche aufgetretene Auffälligkeiten."
    >
      <section v-if="uiLoadFailures.length" class="plugins__diag-block">
        <h3 class="plugins__diag-title">Fehlgeschlagene Plugin-UIs</h3>
        <ul class="plugins__diag-list">
          <li v-for="(r, i) in uiLoadFailures" :key="i">
            <code>{{ r.pluginId }}</code> — {{ r.url }}<template v-if="r.detail">: {{ r.detail }}</template>
          </li>
        </ul>
      </section>

      <section v-if="serviceConflicts.length" class="plugins__diag-block">
        <h3 class="plugins__diag-title">Service-Konflikte</h3>
        <ul class="plugins__diag-list">
          <li v-for="c in serviceConflicts" :key="c.key">
            <code>{{ c.key }}</code> — aktiv: <strong>{{ c.activePluginId ?? 'Host' }}</strong
            >, überschattet: {{ c.shadowedPluginIds.map((p) => p ?? 'Host').join(', ') }}
          </li>
        </ul>
      </section>
    </CalCard>
  </CalPage>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Download, Package, Puzzle } from 'lucide-vue-next'
import { pluginsApi, isPluginActive, type PluginInstallation, type PluginLifecycleResult } from './pluginsApi'

/** Aktiv heißt nicht laufend: Der gewünschte Zustand und der tatsächliche können auseinandergehen. */
function toneOf(row: PluginInstallation): 'success' | 'warning' | 'neutral' | 'danger' {
  // Vor allem anderen: Liegt unter dem Pfad nichts, ist jede Aussage über aktiv oder inaktiv
  // eine über etwas, das gar nicht da ist.
  if (row.assemblyMissing) {
    return 'danger'
  }
  if (!isPluginActive(row.state)) {
    return 'neutral'
  }
  return row.isRunning ? 'success' : 'warning'
}

function labelOf(row: PluginInstallation): string {
  if (row.assemblyMissing) {
    return 'Datei fehlt'
  }
  if (!isPluginActive(row.state)) {
    return 'Inaktiv'
  }
  return row.isRunning ? 'Aktiv' : 'Aktiv, läuft nicht'
}

function stateHintOf(row: PluginInstallation): string | undefined {
  if (row.assemblyMissing) {
    return `Unter dem gespeicherten Pfad liegt keine Datei: ${row.assemblyPath}`
  }
  return isPluginActive(row.state) && !row.isRunning
    ? 'Als aktiv eingetragen, läuft aber nicht — siehe Startprotokoll.'
    : undefined
}
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService, getServiceConflicts } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'
import { getPluginUiLoadResults } from '@/core/extensions/loader'
import CalAlert from '@/core/ui/CalAlert.vue'
import CalBadge from '@/core/ui/CalBadge.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalDataTable from '@/core/ui/CalDataTable.vue'
import CalField from '@/core/ui/CalField.vue'
import CalInput from '@/core/ui/CalInput.vue'
import CalPage from '@/core/ui/CalPage.vue'
import CalPageHeader from '@/core/ui/CalPageHeader.vue'
import type { DataTableColumn } from '@/core/ui/dataTable'
import { confirm } from '@/core/feedback/confirm'
import { toast } from '@/core/feedback/toasts'

const plugins = ref<PluginInstallation[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const notice = ref<string | null>(null)
const busyId = ref<string | null>(null)
const installId = ref('')
const installing = ref(false)
const signatureStates = ref<Record<string, string>>({})

const ctx = useAuthStore().context
// Lifecycle actions map to the backend permissions: activate/deactivate need
// plugin.execute, uninstall plugin.delete, install plugin.create. UI-only gating.
const canExecute = computed(() => hasPermission(ctx.value, 'plugin.execute'))
const canDelete = computed(() => hasPermission(ctx.value, 'plugin.delete'))
const canInstall = computed(() => hasPermission(ctx.value, 'plugin.create'))

const columns: readonly DataTableColumn[] = [
  { key: 'displayName', label: 'Plugin' },
  { key: 'pluginId', label: 'Id', mono: true },
  { key: 'state', label: 'Status', width: '120px' },
  { key: 'signature', label: 'Signatur', width: '150px' },
  { key: 'actions', label: '', align: 'end', width: '250px' },
]

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
  // Signature status is best-effort: it re-verifies each plugin and must not block
  // or fail the list if the report errors.
  try {
    const report = await api.signatureReport()
    signatureStates.value = Object.fromEntries(report.map((r) => [r.pluginId, r.state]))
  } catch {
    signatureStates.value = {}
  }
}

const SIGNATURE_LABELS: Record<string, string> = {
  'signed-trusted': 'Signiert',
  unsigned: 'Unsigniert',
  untrusted: 'Nicht vertraut',
  revoked: 'Widerrufen',
  'content-hash-mismatch': 'Hash-Fehler',
  invalid: 'Ungültig',
}

function signatureLabel(state: string): string {
  return SIGNATURE_LABELS[state] ?? state
}

// Only a trusted signature is reassuring; "unsigned" is a warning rather than a
// failure (local development installs are legitimately unsigned), everything
// else means the artefact cannot be trusted.
function signatureTone(state: string): 'success' | 'warning' | 'danger' {
  if (state === 'signed-trusted') {
    return 'success'
  }
  return state === 'unsigned' ? 'warning' : 'danger'
}

// Shared runner for the state-changing actions: before/after hooks (a plugin may
// veto), warning surfacing, per-row busy state, and a reload afterwards.
async function lifecycleAction(
  verb: string,
  pluginId: string,
  action: () => Promise<PluginLifecycleResult>,
  successMessage: string,
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
    toast.success(successMessage)
    await load()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busyId.value = null
  }
}

function activate(p: PluginInstallation): Promise<void> {
  return lifecycleAction('activate', p.pluginId, () => api.activate(p.pluginId), `„${p.displayName}“ aktiviert.`)
}

function deactivate(p: PluginInstallation): Promise<void> {
  return lifecycleAction('deactivate', p.pluginId, () => api.deactivate(p.pluginId), `„${p.displayName}“ deaktiviert.`)
}

async function uninstall(p: PluginInstallation): Promise<void> {
  const confirmed = await confirm({
    title: `Plugin „${p.displayName}“ deinstallieren?`,
    description: 'Die vom Plugin bereitgestellten Funktionen stehen danach nicht mehr zur Verfügung.',
    confirmLabel: 'Deinstallieren',
    tone: 'danger',
  })
  if (!confirmed) {
    return
  }
  await lifecycleAction('uninstall', p.pluginId, () => api.uninstall(p.pluginId), `„${p.displayName}“ deinstalliert.`)
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
    toast.success(`Plugin „${pluginId}“ installiert.`)
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
.plugins__install {
  margin-bottom: var(--cal-space-4);
}

.plugins__form {
  display: flex;
  align-items: flex-end;
  gap: var(--cal-space-3);
  flex-wrap: wrap;
}

.plugins__form > :deep(.cal-field) {
  flex: 1;
  max-width: 380px;
}

.plugins__notice {
  margin-bottom: var(--cal-space-4);
}

.plugins__actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--cal-space-1);
}

.plugins__diagnostics {
  margin-top: var(--cal-space-6);
}

.plugins__diag-block + .plugins__diag-block {
  margin-top: var(--cal-space-4);
}

.plugins__diag-title {
  margin-bottom: var(--cal-space-2);
  font-size: var(--cal-text-md);
  font-weight: var(--cal-weight-semibold);
  color: var(--cal-danger);
}

.plugins__diag-list {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-1);
  font-size: var(--cal-text-md);
  color: var(--cal-text-secondary);
}

.plugins__diag-list code {
  color: var(--cal-text);
}
</style>
