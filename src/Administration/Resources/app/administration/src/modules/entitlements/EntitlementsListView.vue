<template>
  <CalPage>
    <CalPageHeader title="Berechtigungen" description="Welche Plugins in welchem Bereich genutzt werden dürfen.">
      <template #actions>
        <ExtensionSlot name="entitlements.list.toolbar" />
      </template>
    </CalPageHeader>

    <CalAlert class="entitlements__intro" tone="info" title="So wird ausgewertet">
      Ohne Eintrag gilt die Standard-Berechtigung des Plugins. Ein Workspace-Eintrag sticht einen
      Mandanten-Eintrag, dieser die Plattform-Ebene.
    </CalAlert>

    <CalCard v-if="canManage" class="entitlements__grant" title="Berechtigung erteilen">
      <form class="entitlements__form" @submit.prevent="grant">
        <CalField v-slot="{ id }" label="Plugin-ID">
          <CalInput :id="id" v-model="form.pluginId" name="pluginId" placeholder="communication" />
        </CalField>
        <CalField v-slot="{ id }" label="Mandant" hint="optional">
          <CalInput :id="id" v-model="form.tenantKey" name="tenantKey" />
        </CalField>
        <CalField v-slot="{ id }" label="Workspace" hint="optional">
          <CalInput :id="id" v-model="form.workspaceKey" name="workspaceKey" />
        </CalField>
        <CalButton type="submit" variant="primary" :loading="granting" :disabled="!form.pluginId.trim()">
          Erteilen
        </CalButton>
      </form>
    </CalCard>

    <CalCard flush>
      <CalDataTable
        :columns="columns"
        :rows="entitlements"
        :row-key="rowKey"
        :loading="loading"
        :error="error"
        :empty-icon="KeyRound"
        empty-title="Keine Berechtigungseinträge."
        empty-description="Es gilt überall die Standard-Berechtigung der jeweiligen Plugins."
      >
        <template #cell-scope="{ row }">{{ scopeLabel(row) }}</template>

        <template #cell-isEntitled="{ row }">
          <CalBadge :tone="row.isEntitled ? 'success' : 'danger'" dot>
            {{ row.isEntitled ? 'Berechtigt' : 'Gesperrt' }}
          </CalBadge>
        </template>

        <template #cell-actions="{ row }">
          <div class="entitlements__actions">
            <!-- Both directions are the same reversible switch, so neither gets
                 the destructive treatment. -->
            <CalButton
              v-if="canManage"
              variant="ghost"
              size="sm"
              :disabled="busyKey === rowKey(row)"
              @click="toggle(row)"
            >
              {{ row.isEntitled ? 'Entziehen' : 'Erteilen' }}
            </CalButton>
            <ExtensionSlot name="entitlements.list.row-actions" :ctx="row" />
          </div>
        </template>
      </CalDataTable>
    </CalCard>
  </CalPage>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { KeyRound } from 'lucide-vue-next'
import { entitlementsApi, type Entitlement, type SetEntitlementInput } from './entitlementsApi'
import { scopeLabel } from './entitlementsFormat'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'
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
import { toast } from '@/core/feedback/toasts'

const entitlements = ref<Entitlement[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const busyKey = ref<string | null>(null)
const granting = ref(false)

const form = reactive({ pluginId: '', tenantKey: '', workspaceKey: '' })

const ctx = useAuthStore().context
const canManage = computed(() => hasPermission(ctx.value, 'plugin.execute'))

const columns: readonly DataTableColumn[] = [
  { key: 'pluginId', label: 'Plugin', mono: true },
  { key: 'scope', label: 'Bereich' },
  { key: 'isEntitled', label: 'Status', width: '140px' },
  { key: 'source', label: 'Quelle', mono: true, width: '140px' },
  { key: 'actions', label: '', align: 'end', width: '140px' },
]

// Resolve the entitlements service through the override registry: a plugin may replace it.
const api = useService('entitlementsApi', entitlementsApi)

// A stable per-row identity: plugin + scope (both keys). The API has no surrogate id.
function rowKey(e: { pluginId: string; workspaceKey: string | null; tenantKey: string | null }): string {
  return `${e.pluginId}|${e.tenantKey ?? ''}|${e.workspaceKey ?? ''}`
}

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    entitlements.value = await api.list()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

// Shared grant/revoke path with before/after hooks; verb distinguishes the two.
async function setEntitlement(verb: string, input: SetEntitlementInput): Promise<boolean> {
  const before = await runHook(`entitlements.before-${verb}`, input)
  if (before.canceled) {
    error.value = before.cancelReason ?? 'Aktion abgebrochen.'
    return false
  }
  await api.set(input)
  await runHook(`entitlements.after-${verb}`, {
    pluginId: input.pluginId,
    tenantKey: input.tenantKey,
    workspaceKey: input.workspaceKey,
  })
  return true
}

async function grant(): Promise<void> {
  const pluginId = form.pluginId.trim()
  if (!pluginId || granting.value) {
    return
  }
  error.value = null
  const input: SetEntitlementInput = {
    pluginId,
    tenantKey: form.tenantKey.trim() || null,
    workspaceKey: form.workspaceKey.trim() || null,
    isEntitled: true,
  }
  granting.value = true
  try {
    if (await setEntitlement('grant', input)) {
      form.pluginId = ''
      form.tenantKey = ''
      form.workspaceKey = ''
      toast.success(`Berechtigung für „${pluginId}“ erteilt.`)
      await load()
    }
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    granting.value = false
  }
}

async function toggle(entitlement: Entitlement): Promise<void> {
  const key = rowKey(entitlement)
  if (busyKey.value === key) {
    return
  }
  error.value = null
  const input: SetEntitlementInput = {
    pluginId: entitlement.pluginId,
    tenantKey: entitlement.tenantKey,
    workspaceKey: entitlement.workspaceKey,
    isEntitled: !entitlement.isEntitled,
  }
  busyKey.value = key
  try {
    if (await setEntitlement(entitlement.isEntitled ? 'revoke' : 'grant', input)) {
      toast.success(
        entitlement.isEntitled
          ? `Berechtigung für „${entitlement.pluginId}“ entzogen.`
          : `Berechtigung für „${entitlement.pluginId}“ erteilt.`,
      )
      await load()
    }
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busyKey.value = null
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.entitlements__intro {
  margin-bottom: var(--cal-space-4);
}

.entitlements__grant {
  margin-bottom: var(--cal-space-4);
}

.entitlements__form {
  display: flex;
  align-items: flex-end;
  gap: var(--cal-space-3);
  flex-wrap: wrap;
}

.entitlements__form > :deep(.cal-field) {
  flex: 1;
  min-width: 180px;
}

.entitlements__actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--cal-space-1);
}
</style>
