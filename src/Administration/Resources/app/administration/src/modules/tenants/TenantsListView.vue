<template>
  <section class="tenants">
    <header class="head">
      <h1>Mandanten</h1>
      <div class="head-actions">
        <ExtensionSlot name="tenants.list.toolbar" />
      </div>
    </header>

    <form v-if="canCreate" class="create" @submit.prevent="create">
      <input
        v-model="newKey"
        name="tenantKey"
        class="create-input"
        placeholder="Mandanten-Schlüssel…"
      />
      <input
        v-model="newDisplayName"
        name="displayName"
        class="create-input"
        placeholder="Anzeigename…"
      />
      <BaseButton type="submit" :disabled="creating || !newKey.trim() || !newDisplayName.trim()">
        {{ creating ? 'Legt an…' : 'Anlegen' }}
      </BaseButton>
    </form>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-else-if="loading">Lädt…</p>

    <table v-else class="grid">
      <thead>
        <tr>
          <th>Schlüssel</th>
          <th>Name</th>
          <th>Status</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="t in tenants" :key="t.tenantKey">
          <td class="mono">{{ t.tenantKey }}</td>
          <td>{{ t.displayName }}</td>
          <td>
            <span class="badge" :class="t.isActive ? 'badge-active' : 'badge-inactive'">
              {{ t.isActive ? 'Aktiv' : 'Suspendiert' }}
            </span>
          </td>
          <td class="actions">
            <button
              v-if="canUpdate && t.isActive"
              type="button"
              class="link"
              :disabled="busyKey === t.tenantKey"
              @click="suspend(t)"
            >
              Suspendieren
            </button>
            <button
              v-else-if="canUpdate"
              type="button"
              class="link"
              :disabled="busyKey === t.tenantKey"
              @click="activate(t)"
            >
              Aktivieren
            </button>
            <button
              v-if="canDelete"
              type="button"
              class="link-danger"
              :disabled="busyKey === t.tenantKey"
              @click="remove(t)"
            >
              Löschen
            </button>
            <ExtensionSlot name="tenants.list.row-actions" :ctx="t" />
          </td>
        </tr>
        <tr v-if="!tenants.length">
          <td colspan="4" class="empty">Keine Mandanten vorhanden.</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { tenantsApi, type Tenant } from './tenantsApi'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import BaseButton from '@/core/ui/BaseButton.vue'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'

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
    await load()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    busyKey.value = null
  }
}

function activate(tenant: Tenant): Promise<void> {
  return statusAction('activate', tenant, () => api.activate(tenant.tenantKey))
}

function suspend(tenant: Tenant): Promise<void> {
  return statusAction('suspend', tenant, () => api.suspend(tenant.tenantKey))
}

function remove(tenant: Tenant): Promise<void> {
  if (!window.confirm(`Mandant „${tenant.displayName}“ löschen?`)) {
    return Promise.resolve()
  }
  return statusAction('delete', tenant, () => api.remove(tenant.tenantKey))
}

onMounted(load)
</script>

<style scoped lang="scss">
.tenants {
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

.create {
  display: flex;
  gap: var(--cal-space);
  margin-bottom: calc(var(--cal-space) * 2);
}

.create-input {
  flex: 1;
  max-width: 260px;
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
</style>
