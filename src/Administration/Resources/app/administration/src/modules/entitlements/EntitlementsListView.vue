<template>
  <section class="entitlements">
    <header class="head">
      <h1>Berechtigungen</h1>
      <div class="head-actions">
        <ExtensionSlot name="entitlements.list.toolbar" />
      </div>
    </header>

    <p class="intro">
      Steuert, welche Plugins in einem Bereich genutzt werden dürfen. Ohne Eintrag gilt die
      Standard-Berechtigung. Ein Workspace-Eintrag sticht einen Tenant-Eintrag, dieser die Plattform.
    </p>

    <form v-if="canManage" class="grant" @submit.prevent="grant">
      <input v-model="form.pluginId" name="pluginId" class="grant-input" placeholder="Plugin-ID" />
      <input v-model="form.tenantKey" name="tenantKey" class="grant-input" placeholder="Tenant (optional)" />
      <input v-model="form.workspaceKey" name="workspaceKey" class="grant-input" placeholder="Workspace (optional)" />
      <BaseButton type="submit" :disabled="granting || !form.pluginId.trim()">
        {{ granting ? 'Erteilt…' : 'Erteilen' }}
      </BaseButton>
    </form>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-else-if="loading">Lädt…</p>

    <table v-else class="grid">
      <thead>
        <tr>
          <th>Plugin</th>
          <th>Bereich</th>
          <th>Status</th>
          <th>Quelle</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="e in entitlements" :key="rowKey(e)">
          <td class="mono">{{ e.pluginId }}</td>
          <td>{{ scopeLabel(e) }}</td>
          <td>
            <span class="badge" :class="e.isEntitled ? 'badge-active' : 'badge-inactive'">
              {{ e.isEntitled ? 'Berechtigt' : 'Gesperrt' }}
            </span>
          </td>
          <td class="mono">{{ e.source }}</td>
          <td class="actions">
            <button
              v-if="canManage"
              type="button"
              class="link"
              :disabled="busyKey === rowKey(e)"
              @click="toggle(e)"
            >
              {{ e.isEntitled ? 'Entziehen' : 'Erteilen' }}
            </button>
            <ExtensionSlot name="entitlements.list.row-actions" :ctx="e" />
          </td>
        </tr>
        <tr v-if="!entitlements.length">
          <td colspan="5" class="empty">Keine Berechtigungseinträge — es gilt die Standard-Berechtigung.</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { entitlementsApi, type Entitlement, type SetEntitlementInput } from './entitlementsApi'
import { scopeLabel } from './entitlementsFormat'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import BaseButton from '@/core/ui/BaseButton.vue'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'

const entitlements = ref<Entitlement[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const busyKey = ref<string | null>(null)
const granting = ref(false)

const form = reactive({ pluginId: '', tenantKey: '', workspaceKey: '' })

const ctx = useAuthStore().context
const canManage = computed(() => hasPermission(ctx.value, 'plugin.execute'))

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
.entitlements {
  padding: calc(var(--cal-space) * 3);
}

.head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: var(--cal-space);
}

.head-actions {
  display: flex;
  align-items: center;
  gap: var(--cal-space);
}

.intro {
  color: var(--cal-color-muted);
  margin-bottom: calc(var(--cal-space) * 2);
  max-width: 640px;
}

.grant {
  display: flex;
  gap: var(--cal-space);
  align-items: center;
  flex-wrap: wrap;
  margin-bottom: calc(var(--cal-space) * 2);
}

.grant-input {
  flex: 1;
  min-width: 160px;
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
  color: var(--cal-color-danger);
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

.link:disabled {
  opacity: 0.5;
  cursor: default;
}

.empty {
  color: var(--cal-color-muted);
}

.error {
  color: var(--cal-color-danger);
}
</style>
