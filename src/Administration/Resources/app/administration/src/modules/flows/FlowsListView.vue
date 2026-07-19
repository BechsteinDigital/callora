<template>
  <section class="flows">
    <header class="head">
      <h1>Flows</h1>
      <div class="head-actions">
        <ExtensionSlot name="flows.list.toolbar" />
      </div>
    </header>

    <p v-if="error" class="error">{{ error }}</p>

    <p v-if="loading">Lädt…</p>
    <p v-else-if="!activeWorkspace" class="empty">Kein Workspace ausgewählt.</p>

    <div v-else class="body">
      <table class="grid">
        <thead>
          <tr>
            <th>Name</th>
            <th>Trigger</th>
            <th>Priorität</th>
            <th>Status</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="flow in flows" :key="flow.id">
            <td>{{ flow.name }}</td>
            <td class="mono">{{ flow.triggerEvent }}</td>
            <td>{{ flow.priority }}</td>
            <td>
              <span class="badge" :class="flow.isActive ? 'badge-active' : 'badge-inactive'">
                {{ flow.isActive ? 'Aktiv' : 'Inaktiv' }}
              </span>
            </td>
            <td class="actions">
              <button
                v-if="canManage"
                type="button"
                class="link"
                :disabled="busyId === flow.id"
                @click="startEdit(flow)"
              >
                Bearbeiten
              </button>
              <button
                v-if="canManage"
                type="button"
                class="link-danger"
                :disabled="busyId === flow.id"
                @click="remove(flow)"
              >
                Löschen
              </button>
              <ExtensionSlot name="flows.list.row-actions" :ctx="flow" />
            </td>
          </tr>
          <tr v-if="!flows.length">
            <td colspan="5" class="empty">Keine Flows in diesem Workspace.</td>
          </tr>
        </tbody>
      </table>

      <div v-if="nextCursor" class="more">
        <button type="button" class="link" :disabled="loadingMore" @click="loadMore">
          {{ loadingMore ? 'Lädt…' : `Mehr laden (${flows.length}${total ? ` von ${total}` : ''})` }}
        </button>
      </div>

      <form v-if="canManage" class="flow-form" @submit.prevent="save">
        <h3>{{ editingId ? 'Flow bearbeiten' : 'Flow anlegen' }}</h3>
        <div class="fields">
          <label>Name
            <BaseInput v-model="form.name" name="flowName" />
          </label>
          <label>Trigger-Event
            <BaseInput v-model="form.triggerEvent" name="flowTrigger" />
          </label>
          <label>Priorität
            <input v-model.number="form.priority" type="number" name="flowPriority" class="num" />
          </label>
          <label class="check">
            <input type="checkbox" v-model="form.isActive" name="flowActive" />
            Aktiv
          </label>
        </div>
        <label class="json">Bedingungen <span class="hint">(JSON, optional)</span>
          <textarea v-model="form.conditionsText" name="flowConditions" class="code" rows="4" />
        </label>
        <label class="json">Aktionen <span class="hint">(JSON-Array)</span>
          <textarea v-model="form.actionsText" name="flowActions" class="code" rows="4" />
        </label>
        <div class="buttons">
          <BaseButton type="submit" :disabled="saving || !canSubmit">
            {{ editingId ? 'Speichern' : 'Anlegen' }}
          </BaseButton>
          <button v-if="editingId" type="button" class="link" @click="resetForm">Abbrechen</button>
        </div>
      </form>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { flowsApi, type Flow, type UpsertFlowInput } from './flowsApi'
import { parseJsonField, prettyJson } from './flowsFormat'
import { useWorkspaceContext } from '@/core/workspace/workspaceContext'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import BaseButton from '@/core/ui/BaseButton.vue'
import BaseInput from '@/core/ui/BaseInput.vue'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'

const ctx = useAuthStore().context
const canManage = computed(() => hasPermission(ctx.value, 'flow.manage'))

// The workspace comes from the global context (topbar switcher or the bound
// admin's fixed workspace) — no per-view picker.
const { activeWorkspace, ensure: ensureWorkspace } = useWorkspaceContext()

// Resolve the flows service through the override registry: a plugin may replace it.
const api = useService('flowsApi', flowsApi)

const flows = ref<Flow[]>([])
const loading = ref(true)
const loadingMore = ref(false)
const error = ref<string | null>(null)
const total = ref(0)
const nextCursor = ref<string | null>(null)
const busyId = ref<string | null>(null)
const saving = ref(false)

const editingId = ref<string | null>(null)
const form = reactive({
  name: '',
  triggerEvent: '',
  priority: 100,
  isActive: true,
  conditionsText: '',
  actionsText: '[]',
})

const canSubmit = computed(
  () => activeWorkspace.value !== '' && form.name.trim() !== '' && form.triggerEvent.trim() !== '',
)

async function loadFlows(): Promise<void> {
  if (!activeWorkspace.value) {
    flows.value = []
    loading.value = false
    return
  }
  loading.value = true
  error.value = null
  try {
    const page = await api.list(activeWorkspace.value)
    flows.value = page.items
    total.value = page.total
    nextCursor.value = page.nextCursor
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

async function loadMore(): Promise<void> {
  if (!nextCursor.value || loadingMore.value) {
    return
  }
  loadingMore.value = true
  error.value = null
  try {
    const page = await api.list(activeWorkspace.value, nextCursor.value)
    flows.value = [...flows.value, ...page.items]
    total.value = page.total
    nextCursor.value = page.nextCursor
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loadingMore.value = false
  }
}

function resetForm(): void {
  editingId.value = null
  form.name = ''
  form.triggerEvent = ''
  form.priority = 100
  form.isActive = true
  form.conditionsText = ''
  form.actionsText = '[]'
}

function startEdit(flow: Flow): void {
  editingId.value = flow.id
  form.name = flow.name
  form.triggerEvent = flow.triggerEvent
  form.priority = flow.priority
  form.isActive = flow.isActive
  form.conditionsText = prettyJson(flow.conditionsJson)
  form.actionsText = prettyJson(flow.actionsJson)
}

async function save(): Promise<void> {
  if (!canSubmit.value || saving.value) {
    return
  }
  error.value = null

  let input: UpsertFlowInput
  try {
    input = {
      name: form.name.trim(),
      triggerEvent: form.triggerEvent.trim(),
      conditions: parseJsonField(form.conditionsText, null, 'Bedingungen'),
      actions: parseJsonField(form.actionsText, [], 'Aktionen'),
      isActive: form.isActive,
      priority: form.priority,
    }
  } catch (e) {
    error.value = (e as Error).message
    return
  }

  const before = await runHook('flows.before-save', { workspaceKey: activeWorkspace.value, isEdit: editingId.value !== null })
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Speichern abgebrochen.'
    return
  }

  saving.value = true
  try {
    if (editingId.value) {
      await api.update(activeWorkspace.value, editingId.value, input)
    } else {
      await api.create(activeWorkspace.value, input)
    }
    await runHook('flows.after-save', { workspaceKey: activeWorkspace.value, name: input.name })
    resetForm()
    await loadFlows()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    saving.value = false
  }
}

async function remove(flow: Flow): Promise<void> {
  if (busyId.value === flow.id) {
    return
  }
  if (!window.confirm(`Flow „${flow.name}“ löschen?`)) {
    return
  }
  error.value = null
  const before = await runHook('flows.before-delete', { workspaceKey: activeWorkspace.value, id: flow.id })
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Löschen abgebrochen.'
    return
  }
  busyId.value = flow.id
  try {
    await api.remove(activeWorkspace.value, flow.id)
    await runHook('flows.after-delete', { workspaceKey: activeWorkspace.value, id: flow.id })
    if (editingId.value === flow.id) {
      resetForm()
    }
    await loadFlows()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busyId.value = null
  }
}

// Reload whenever the active workspace resolves or the operator switches it (the
// immediate run covers a fixed admin's initial load; the operator's first real
// load comes when ensure() populates the selection below).
watch(
  activeWorkspace,
  () => {
    resetForm()
    void loadFlows()
  },
  { immediate: true },
)

onMounted(() => {
  void ensureWorkspace().catch((e) => {
    error.value = (e as Error).message
    loading.value = false
  })
})
</script>

<style scoped lang="scss">
.flows {
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

.num {
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
  border: 1px solid currentColor;
}

.badge-active {
  color: var(--cal-color-accent);
}

.badge-inactive {
  color: var(--cal-color-muted);
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

.more {
  margin-top: var(--cal-space);
}

.flow-form {
  margin-top: calc(var(--cal-space) * 3);
  max-width: 640px;
}

.flow-form h3 {
  font-size: 1em;
  margin-bottom: var(--cal-space);
}

.fields {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--cal-space) calc(var(--cal-space) * 2);
  margin-bottom: var(--cal-space);
}

.fields label,
.json {
  display: flex;
  flex-direction: column;
  gap: 4px;
  color: var(--cal-color-muted);
}

.fields label.check {
  flex-direction: row;
  align-items: center;
  gap: var(--cal-space);
}

.json {
  margin-bottom: var(--cal-space);
}

.code {
  padding: var(--cal-space);
  border: 1px solid var(--cal-color-muted);
  border-radius: var(--cal-radius);
  background: var(--cal-color-surface);
  color: var(--cal-color-text);
  font-family: var(--cal-font-mono, monospace);
  resize: vertical;
}

.hint {
  font-size: 0.85em;
}

.buttons {
  display: flex;
  align-items: center;
  gap: calc(var(--cal-space) * 2);
  margin-top: var(--cal-space);
}

.empty {
  color: var(--cal-color-muted);
}

.error {
  color: var(--cal-color-danger);
}
</style>
