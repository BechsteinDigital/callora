<template>
  <CalPage>
    <CalPageHeader title="Flows" description="Regeln, die auf Ereignisse des aktiven Workspaces reagieren.">
      <template #actions>
        <ExtensionSlot name="flows.list.toolbar" />
      </template>
    </CalPageHeader>

    <CalCard v-if="!loading && !activeWorkspace">
      <CalEmptyState
        :icon="Boxes"
        title="Kein Workspace ausgewählt."
        description="Wählen Sie oben rechts einen Workspace, um dessen Flows zu sehen."
      />
    </CalCard>

    <template v-else>
      <CalCard flush>
        <CalDataTable
          :columns="columns"
          :rows="flows"
          row-key="id"
          :loading="loading"
          :error="error"
          :empty-icon="Workflow"
          empty-title="Keine Flows in diesem Workspace."
          empty-description="Ein Flow verknüpft ein Ereignis mit Aktionen — etwa „Anruf beendet“ mit „Webhook senden“."
        >
          <template #cell-isActive="{ row }">
            <CalBadge :tone="row.isActive ? 'success' : 'neutral'" dot>
              {{ row.isActive ? 'Aktiv' : 'Inaktiv' }}
            </CalBadge>
          </template>

          <template #cell-actions="{ row }">
            <div class="flows__actions">
              <CalButton v-if="canManage" variant="ghost" size="sm" :disabled="busyId === row.id" @click="startEdit(row)">
                Bearbeiten
              </CalButton>
              <CalButton
                v-if="canManage"
                variant="danger-ghost"
                size="sm"
                :disabled="busyId === row.id"
                @click="remove(row)"
              >
                Löschen
              </CalButton>
              <ExtensionSlot name="flows.list.row-actions" :ctx="row" />
            </div>
          </template>
        </CalDataTable>

        <template v-if="nextCursor" #footer>
          <CalButton :loading="loadingMore" @click="loadMore">
            Mehr laden ({{ flows.length }}{{ total ? ` von ${total}` : '' }})
          </CalButton>
        </template>
      </CalCard>

      <CalCard
        v-if="canManage"
        class="flows__editor"
        :title="editingId ? 'Flow bearbeiten' : 'Flow anlegen'"
        description="Bedingungen und Aktionen werden als JSON hinterlegt."
      >
        <form class="flows__form" @submit.prevent="save">
          <div class="flows__fields">
            <CalField v-slot="{ id }" label="Name" required>
              <CalInput :id="id" v-model="form.name" name="flowName" />
            </CalField>
            <CalField v-slot="{ id }" label="Trigger-Event" required>
              <CalInput :id="id" v-model="form.triggerEvent" name="flowTrigger" placeholder="call.completed" />
            </CalField>
            <CalField v-slot="{ id }" label="Priorität" hint="kleiner = früher">
              <CalInput :id="id" v-model="priorityText" type="number" name="flowPriority" />
            </CalField>
            <CalField label="Zustand">
              <CalSwitch v-model="form.isActive" name="flowActive">Aktiv</CalSwitch>
            </CalField>
          </div>

          <CalField v-slot="{ id }" label="Bedingungen" hint="JSON, optional">
            <CalTextarea :id="id" v-model="form.conditionsText" name="flowConditions" mono :rows="4" />
          </CalField>

          <CalField v-slot="{ id }" label="Aktionen" hint="JSON-Array">
            <CalTextarea :id="id" v-model="form.actionsText" name="flowActions" mono :rows="4" />
          </CalField>
        </form>

        <template #footer>
          <div class="buttons">
            <CalButton v-if="editingId" variant="ghost" @click="resetForm">Abbrechen</CalButton>
            <CalButton variant="primary" :loading="saving" :disabled="!canSubmit" @click="save">
              {{ editingId ? 'Speichern' : 'Anlegen' }}
            </CalButton>
          </div>
        </template>
      </CalCard>
    </template>
  </CalPage>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { Boxes, Workflow } from 'lucide-vue-next'
import { flowsApi, type Flow, type UpsertFlowInput } from './flowsApi'
import { parseJsonField, prettyJson } from './flowsFormat'
import { useWorkspaceContext } from '@/core/workspace/workspaceContext'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'
import CalBadge from '@/core/ui/CalBadge.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalDataTable from '@/core/ui/CalDataTable.vue'
import CalEmptyState from '@/core/ui/CalEmptyState.vue'
import CalField from '@/core/ui/CalField.vue'
import CalInput from '@/core/ui/CalInput.vue'
import CalPage from '@/core/ui/CalPage.vue'
import CalPageHeader from '@/core/ui/CalPageHeader.vue'
import CalSwitch from '@/core/ui/CalSwitch.vue'
import CalTextarea from '@/core/ui/CalTextarea.vue'
import type { DataTableColumn } from '@/core/ui/dataTable'
import { confirm } from '@/core/feedback/confirm'
import { toast } from '@/core/feedback/toasts'

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

const columns: readonly DataTableColumn[] = [
  { key: 'name', label: 'Name' },
  { key: 'triggerEvent', label: 'Trigger', mono: true },
  { key: 'priority', label: 'Priorität', width: '110px' },
  { key: 'isActive', label: 'Status', width: '120px' },
  { key: 'actions', label: '', align: 'end', width: '210px' },
]

const editingId = ref<string | null>(null)
const form = reactive({
  name: '',
  triggerEvent: '',
  priority: 100,
  isActive: true,
  conditionsText: '',
  actionsText: '[]',
})

// CalInput speaks strings; the priority is a number in the payload. Bridging here
// keeps the number-vs-text conversion in one place instead of in the template.
const priorityText = computed({
  get: () => String(form.priority),
  set: (value: string) => {
    const parsed = Number(value)
    form.priority = Number.isFinite(parsed) ? parsed : 0
  },
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

  const before = await runHook('flows.before-save', {
    workspaceKey: activeWorkspace.value,
    isEdit: editingId.value !== null,
  })
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
    toast.success(editingId.value ? `Flow „${input.name}“ gespeichert.` : `Flow „${input.name}“ angelegt.`)
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
  const confirmed = await confirm({
    title: `Flow „${flow.name}“ löschen?`,
    description: `Auf „${flow.triggerEvent}“ wird danach nicht mehr reagiert.`,
    confirmLabel: 'Löschen',
    tone: 'danger',
  })
  if (!confirmed) {
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
    toast.success(`Flow „${flow.name}“ gelöscht.`)
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
.flows__actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--cal-space-1);
}

.flows__editor {
  margin-top: var(--cal-space-4);
}

.flows__form {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-5);
}

.flows__fields {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: var(--cal-space-4);
}

.buttons {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
}
</style>
