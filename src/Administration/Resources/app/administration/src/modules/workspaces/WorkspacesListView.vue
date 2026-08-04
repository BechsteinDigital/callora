<template>
  <CalPage>
    <CalPageHeader title="Workspaces" description="Die Arbeitsbereiche, in denen die Plattform betrieben wird.">
      <template #actions>
        <ExtensionSlot name="workspaces.list.toolbar" />
        <CalButton v-if="canManage" variant="primary" :icon="Plus" to="/workspaces/new">Neu anlegen</CalButton>
      </template>
    </CalPageHeader>

    <CalCard flush>
      <CalDataTable
        :columns="columns"
        :rows="workspaces"
        row-key="workspaceKey"
        :loading="loading"
        :error="error"
        :empty-icon="Boxes"
        empty-title="Keine Workspaces vorhanden."
        empty-description="Ein Workspace bündelt Daten, Mitglieder und Surfaces eines Betriebs."
      >
        <template #cell-isActive="{ row }">
          <CalBadge :tone="row.isActive ? 'success' : 'neutral'" dot>
            {{ row.isActive ? 'Aktiv' : 'Inaktiv' }}
          </CalBadge>
        </template>

        <template #cell-actions="{ row }">
          <div class="workspaces__actions">
            <CalButton v-if="canManage" variant="ghost" size="sm" :to="`/workspaces/${row.workspaceKey}`">
              Bearbeiten
            </CalButton>
            <CalButton v-if="canDelete" variant="danger-ghost" size="sm" @click="remove(row)">Löschen</CalButton>
            <ExtensionSlot name="workspaces.list.row-actions" :ctx="row" />
          </div>
        </template>

        <template v-if="canManage" #empty-action>
          <CalButton variant="primary" :icon="Plus" to="/workspaces/new">Workspace anlegen</CalButton>
        </template>
      </CalDataTable>
    </CalCard>
  </CalPage>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Boxes, Plus } from 'lucide-vue-next'
import { workspacesApi, type Workspace } from './workspacesApi'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'
import CalBadge from '@/core/ui/CalBadge.vue'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalDataTable from '@/core/ui/CalDataTable.vue'
import CalPage from '@/core/ui/CalPage.vue'
import CalPageHeader from '@/core/ui/CalPageHeader.vue'
import type { DataTableColumn } from '@/core/ui/dataTable'
import { confirm } from '@/core/feedback/confirm'
import { toast } from '@/core/feedback/toasts'

const workspaces = ref<Workspace[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const ctx = useAuthStore().context
// Create/edit go through the PUT upsert, which the backend gates on workspace.update;
// delete needs workspace.delete. UI-only gating, server stays authoritative.
const canManage = computed(() => hasPermission(ctx.value, 'workspace.update'))
const canDelete = computed(() => hasPermission(ctx.value, 'workspace.delete'))

const columns: readonly DataTableColumn[] = [
  { key: 'displayName', label: 'Workspace' },
  { key: 'workspaceKey', label: 'Schlüssel', mono: true },
  { key: 'workspaceType', label: 'Typ', width: '140px' },
  { key: 'isActive', label: 'Status', width: '120px' },
  { key: 'actions', label: '', align: 'end', width: '210px' },
]

// Resolve the workspaces service through the override registry: a plugin may replace it.
const api = useService('workspacesApi', workspacesApi)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    workspaces.value = await api.list()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

async function remove(workspace: Workspace): Promise<void> {
  const confirmed = await confirm({
    title: `Workspace „${workspace.displayName}“ löschen?`,
    description: 'Alle zugehörigen Daten — Mitglieder, Surfaces, Medien und Flows — gehen unwiderruflich verloren.',
    confirmLabel: 'Löschen',
    tone: 'danger',
  })
  if (!confirmed) {
    return
  }
  error.value = null
  const before = await runHook('workspaces.before-delete', { workspaceKey: workspace.workspaceKey })
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Löschen abgebrochen.'
    return
  }
  try {
    await api.remove(workspace.workspaceKey)
    await runHook('workspaces.after-delete', { workspaceKey: workspace.workspaceKey })
    toast.success(`Workspace „${workspace.displayName}“ gelöscht.`)
    await load()
  } catch (e) {
    error.value = (e as Error).message
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.workspaces__actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--cal-space-1);
}
</style>
