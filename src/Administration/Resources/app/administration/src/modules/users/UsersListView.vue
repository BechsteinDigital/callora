<template>
  <CalPage>
    <CalPageHeader title="Benutzer" description="Konten, die sich an der Administration anmelden dürfen.">
      <template #actions>
        <ExtensionSlot name="users.list.toolbar" />
        <CalButton v-if="canCreate" variant="primary" :icon="Plus" to="/users/new">Neu anlegen</CalButton>
      </template>
    </CalPageHeader>

    <CalCard flush>
      <CalDataTable
        :columns="columns"
        :rows="users"
        row-key="externalId"
        :loading="loading"
        :error="error"
        :empty-icon="Users"
        empty-title="Keine Benutzer vorhanden."
        empty-description="Legen Sie ein erstes Konto an, um die Administration freizugeben."
      >
        <template #cell-role="{ row }">
          <CalBadge v-if="roleFor(row.externalId) !== '—'" tone="neutral">{{ roleFor(row.externalId) }}</CalBadge>
          <span v-else>—</span>
        </template>

        <template #cell-hasPassword="{ row }">
          <CalBadge :tone="row.hasPassword ? 'success' : 'warning'" dot>
            {{ row.hasPassword ? 'gesetzt' : 'fehlt' }}
          </CalBadge>
        </template>

        <template #cell-actions="{ row }">
          <div class="users__actions">
            <CalButton v-if="canUpdate" variant="ghost" size="sm" :to="`/users/${row.externalId}`">
              Bearbeiten
            </CalButton>
            <CalButton v-if="canDelete" variant="danger-ghost" size="sm" @click="remove(row)">Löschen</CalButton>
            <ExtensionSlot name="users.list.row-actions" :ctx="row" />
          </div>
        </template>

        <template v-if="canCreate" #empty-action>
          <CalButton variant="primary" :icon="Plus" to="/users/new">Benutzer anlegen</CalButton>
        </template>
      </CalDataTable>
    </CalCard>
  </CalPage>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Plus, Users } from 'lucide-vue-next'
import { usersApi, type BackendUser } from './usersApi'
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

const users = ref<BackendUser[]>([])
const roleAssignments = ref<Record<string, string>>({})
const loading = ref(true)
const error = ref<string | null>(null)

const ctx = useAuthStore().context
const canCreate = computed(() => hasPermission(ctx.value, 'user.create'))
const canUpdate = computed(() => hasPermission(ctx.value, 'user.update'))
const canDelete = computed(() => hasPermission(ctx.value, 'user.delete'))
const canReadRoles = computed(() => hasPermission(ctx.value, 'role.read'))

// The role column disappears entirely for a caller without role.read, so header
// and cells cannot disagree about what is shown.
const columns = computed<DataTableColumn[]>(() => [
  { key: 'externalId', label: 'Login' },
  { key: 'email', label: 'E-Mail' },
  { key: 'displayName', label: 'Name' },
  { key: 'role', label: 'Rolle', hidden: !canReadRoles.value },
  { key: 'hasPassword', label: 'Passwort', width: '130px' },
  { key: 'actions', label: '', align: 'end', width: '210px' },
])

// Resolve the user service through the override registry: a plugin may replace it.
const api = useService('usersApi', usersApi)

function roleFor(userId: string): string {
  return roleAssignments.value[userId] ?? '—'
}

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    users.value = await api.list()
    // Role assignments live behind role.read — only fetch them when allowed,
    // otherwise the request would 403 and mask the user list.
    roleAssignments.value = canReadRoles.value ? await api.listRoleAssignments() : {}
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

async function remove(user: BackendUser): Promise<void> {
  const confirmed = await confirm({
    title: `Benutzer „${user.externalId}“ löschen?`,
    description: 'Das anonymisiert auch den Audit-Trail (Art. 17 DSGVO) und lässt sich nicht rückgängig machen.',
    confirmLabel: 'Löschen',
    tone: 'danger',
  })
  if (!confirmed) {
    return
  }
  const before = await runHook('users.before-delete', { userId: user.externalId })
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Löschen abgebrochen.'
    return
  }
  try {
    await api.remove(user.externalId)
    await runHook('users.after-delete', { userId: user.externalId })
    toast.success(`Benutzer „${user.externalId}“ gelöscht.`)
    await load()
  } catch (e) {
    error.value = (e as Error).message
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.users__actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--cal-space-1);
}
</style>
