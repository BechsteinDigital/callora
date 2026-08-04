<template>
  <CalPage>
    <CalPageHeader title="Rollen" description="Rechtebündel, die Benutzern zugewiesen werden.">
      <template #actions>
        <ExtensionSlot name="roles.list.toolbar" />
        <CalButton v-if="canManage" variant="primary" :icon="Plus" to="/roles/new">Neu anlegen</CalButton>
      </template>
    </CalPageHeader>

    <CalCard flush>
      <CalDataTable
        :columns="columns"
        :rows="roles"
        row-key="role"
        :loading="loading"
        :error="error"
        :empty-icon="ShieldCheck"
        empty-title="Keine Rollen vorhanden."
        empty-description="Ohne Rolle kann ein Benutzer nichts einsehen oder ändern."
      >
        <template #cell-role="{ row }">
          <span class="roles__name">
            {{ row.role }}
            <CalBadge v-if="row.role === SYSTEM_ROLE" tone="info" variant="outline">System</CalBadge>
          </span>
        </template>

        <template #cell-permissions="{ row }">
          <CalBadge :tone="row.permissions.includes('*') ? 'accent' : 'neutral'">
            {{ describePermissions(row) }}
          </CalBadge>
        </template>

        <template #cell-actions="{ row }">
          <div v-if="canManage && row.role !== SYSTEM_ROLE" class="roles__actions">
            <CalButton variant="ghost" size="sm" :to="`/roles/${row.role}`">Bearbeiten</CalButton>
            <CalButton variant="danger-ghost" size="sm" @click="remove(row)">Löschen</CalButton>
          </div>
          <span v-else-if="row.role === SYSTEM_ROLE" class="roles__locked">unveränderlich</span>
        </template>

        <template v-if="canManage" #empty-action>
          <CalButton variant="primary" :icon="Plus" to="/roles/new">Rolle anlegen</CalButton>
        </template>
      </CalDataTable>
    </CalCard>
  </CalPage>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Plus, ShieldCheck } from 'lucide-vue-next'
import { rolesApi, SYSTEM_ROLE, type Role } from './rolesApi'
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

const roles = ref<Role[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const ctx = useAuthStore().context
const canManage = computed(() => hasPermission(ctx.value, 'role.update'))

const columns: readonly DataTableColumn[] = [
  { key: 'role', label: 'Rolle' },
  { key: 'permissions', label: 'Rechte', width: '200px' },
  { key: 'actions', label: '', align: 'end', width: '210px' },
]

// Resolve the roles service through the override registry: a plugin may replace it.
const api = useService('rolesApi', rolesApi)

function describePermissions(role: Role): string {
  if (role.permissions.includes('*')) {
    return 'alle (*)'
  }
  return `${role.permissions.length} Recht${role.permissions.length === 1 ? '' : 'e'}`
}

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    roles.value = await api.list()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

async function remove(role: Role): Promise<void> {
  const confirmed = await confirm({
    title: `Rolle „${role.role}“ löschen?`,
    description: 'Benutzer mit dieser Rolle verlieren die darüber gewährten Rechte.',
    confirmLabel: 'Löschen',
    tone: 'danger',
  })
  if (!confirmed) {
    return
  }
  const before = await runHook('roles.before-delete', { role: role.role })
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Löschen abgebrochen.'
    return
  }
  try {
    await api.remove(role.role)
    await runHook('roles.after-delete', { role: role.role })
    toast.success(`Rolle „${role.role}“ gelöscht.`)
    await load()
  } catch (e) {
    error.value = (e as Error).message
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.roles__name {
  display: inline-flex;
  align-items: center;
  gap: var(--cal-space-2);
}

.roles__actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--cal-space-1);
}

.roles__locked {
  display: block;
  text-align: right;
  font-size: var(--cal-text-sm);
  color: var(--cal-text-muted);
}
</style>
