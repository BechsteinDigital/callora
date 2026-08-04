<template>
  <CalPage>
    <CalPageHeader title="Mandanten" description="Die abrechnenden Einheiten der Plattform.">
      <template #actions>
        <ExtensionSlot name="tenants.list.toolbar" />
      </template>
    </CalPageHeader>

    <CalCard v-if="canCreate" class="tenants__create" title="Mandant anlegen">
      <form class="tenants__form" @submit.prevent="create">
        <CalField v-slot="{ id }" label="Schlüssel" hint="technisch, unveränderlich">
          <CalInput :id="id" v-model="newKey" name="tenantKey" placeholder="acme-gmbh" />
        </CalField>
        <CalField v-slot="{ id }" label="Anzeigename">
          <CalInput :id="id" v-model="newDisplayName" name="displayName" placeholder="ACME GmbH" />
        </CalField>
        <CalButton
          type="submit"
          variant="primary"
          :icon="Plus"
          :loading="creating"
          :disabled="!newKey.trim() || !newDisplayName.trim()"
        >
          Anlegen
        </CalButton>
      </form>
    </CalCard>

    <CalCard flush>
      <CalDataTable
        :columns="columns"
        :rows="tenants"
        row-key="tenantKey"
        :loading="loading"
        :error="error"
        :empty-icon="Building2"
        empty-title="Keine Mandanten vorhanden."
        empty-description="Ein Mandant bündelt Workspaces zu einer abrechnenden Einheit."
      >
        <template #cell-isActive="{ row }">
          <CalBadge :tone="row.isActive ? 'success' : 'warning'" dot>
            {{ row.isActive ? 'Aktiv' : 'Suspendiert' }}
          </CalBadge>
        </template>

        <template #cell-actions="{ row }">
          <div class="tenants__actions">
            <CalButton
              v-if="canUpdate"
              variant="ghost"
              size="sm"
              :disabled="busyKey === row.tenantKey"
              @click="row.isActive ? suspend(row) : activate(row)"
            >
              {{ row.isActive ? 'Suspendieren' : 'Aktivieren' }}
            </CalButton>
            <CalButton
              v-if="canDelete"
              variant="danger-ghost"
              size="sm"
              :disabled="busyKey === row.tenantKey"
              @click="remove(row)"
            >
              Löschen
            </CalButton>
            <ExtensionSlot name="tenants.list.row-actions" :ctx="row" />
          </div>
        </template>
      </CalDataTable>
    </CalCard>
  </CalPage>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { Building2, Plus } from 'lucide-vue-next'
import { tenantsApi, type Tenant } from './tenantsApi'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'
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

const tenants = ref<Tenant[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const busyKey = ref<string | null>(null)
const newKey = ref('')
const newDisplayName = ref('')
const creating = ref(false)

const ctx = useAuthStore().context
const canCreate = computed(() => hasPermission(ctx.value, 'tenant.create'))
const canUpdate = computed(() => hasPermission(ctx.value, 'tenant.update'))
const canDelete = computed(() => hasPermission(ctx.value, 'tenant.delete'))

const columns: readonly DataTableColumn[] = [
  { key: 'tenantKey', label: 'Schlüssel', mono: true },
  { key: 'displayName', label: 'Name' },
  { key: 'isActive', label: 'Status', width: '150px' },
  { key: 'actions', label: '', align: 'end', width: '250px' },
]

// Resolve the tenants service through the override registry: a plugin may replace it.
const api = useService('tenantsApi', tenantsApi)

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    tenants.value = await api.list()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

async function create(): Promise<void> {
  const tenantKey = newKey.value.trim()
  const displayName = newDisplayName.value.trim()
  if (!tenantKey || !displayName) {
    return
  }
  error.value = null
  const draft = { tenantKey, displayName }
  const before = await runHook('tenants.before-create', draft)
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Anlegen abgebrochen.'
    return
  }
  creating.value = true
  try {
    await api.create(draft.tenantKey, draft.displayName)
    newKey.value = ''
    newDisplayName.value = ''
    await runHook('tenants.after-create', { tenantKey })
    toast.success(`Mandant „${displayName}“ angelegt.`)
    await load()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    creating.value = false
  }
}

async function statusAction(
  verb: string,
  tenant: Tenant,
  action: () => Promise<void>,
  successMessage: string,
): Promise<void> {
  error.value = null
  const before = await runHook(`tenants.before-${verb}`, { tenantKey: tenant.tenantKey })
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Aktion abgebrochen.'
    return
  }
  busyKey.value = tenant.tenantKey
  try {
    await action()
    await runHook(`tenants.after-${verb}`, { tenantKey: tenant.tenantKey })
    toast.success(successMessage)
    await load()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busyKey.value = null
  }
}

function activate(tenant: Tenant): Promise<void> {
  return statusAction(
    'activate',
    tenant,
    () => api.activate(tenant.tenantKey),
    `Mandant „${tenant.displayName}“ aktiviert.`,
  )
}

function suspend(tenant: Tenant): Promise<void> {
  return statusAction(
    'suspend',
    tenant,
    () => api.suspend(tenant.tenantKey),
    `Mandant „${tenant.displayName}“ suspendiert.`,
  )
}

async function remove(tenant: Tenant): Promise<void> {
  const confirmed = await confirm({
    title: `Mandant „${tenant.displayName}“ löschen?`,
    description: 'Die Zuordnung der Workspaces zu diesem Mandanten geht verloren.',
    confirmLabel: 'Löschen',
    tone: 'danger',
  })
  if (!confirmed) {
    return
  }
  await statusAction(
    'delete',
    tenant,
    () => api.remove(tenant.tenantKey),
    `Mandant „${tenant.displayName}“ gelöscht.`,
  )
}

onMounted(load)
</script>

<style scoped lang="scss">
.tenants__create {
  margin-bottom: var(--cal-space-4);
}

.tenants__form {
  display: flex;
  align-items: flex-end;
  gap: var(--cal-space-3);
  flex-wrap: wrap;
}

.tenants__form > :deep(.cal-field) {
  flex: 1;
  min-width: 200px;
}

.tenants__actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--cal-space-1);
}
</style>
