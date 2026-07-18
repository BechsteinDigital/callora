<template>
  <section class="workspaces">
    <header class="head">
      <h1>Workspaces</h1>
      <div class="head-actions">
        <ExtensionSlot name="workspaces.list.toolbar" />
        <RouterLink v-if="canManage" class="new" to="/workspaces/new">Neu anlegen</RouterLink>
      </div>
    </header>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-else-if="loading">Lädt…</p>

    <table v-else class="grid">
      <thead>
        <tr>
          <th>Workspace</th>
          <th>Schlüssel</th>
          <th>Typ</th>
          <th>Status</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="w in workspaces" :key="w.workspaceKey">
          <td>{{ w.displayName }}</td>
          <td class="mono">{{ w.workspaceKey }}</td>
          <td>{{ w.workspaceType }}</td>
          <td>
            <span class="badge" :class="w.isActive ? 'badge-active' : 'badge-inactive'">
              {{ w.isActive ? 'Aktiv' : 'Inaktiv' }}
            </span>
          </td>
          <td class="actions">
            <RouterLink v-if="canManage" :to="`/workspaces/${w.workspaceKey}`">Bearbeiten</RouterLink>
            <button v-if="canDelete" type="button" class="link-danger" @click="remove(w)">Löschen</button>
            <ExtensionSlot name="workspaces.list.row-actions" :ctx="w" />
          </td>
        </tr>
        <tr v-if="!workspaces.length">
          <td colspan="5" class="empty">Keine Workspaces vorhanden.</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { workspacesApi, type Workspace } from './workspacesApi'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'

const workspaces = ref<Workspace[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const ctx = useAuthStore().context
// Create/edit go through the PUT upsert, which the backend gates on workspace.update;
// delete needs workspace.delete. UI-only gating, server stays authoritative.
const canManage = computed(() => hasPermission(ctx.value, 'workspace.update'))
const canDelete = computed(() => hasPermission(ctx.value, 'workspace.delete'))

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
  if (!window.confirm(`Workspace „${workspace.displayName}“ und alle zugehörigen Daten löschen?`)) {
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
    await load()
  } catch (e) {
    error.value = (e as Error).message
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.workspaces {
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

.new {
  text-decoration: none;
  padding: var(--cal-space) calc(var(--cal-space) * 1.5);
  border-radius: var(--cal-radius);
  background: var(--cal-color-accent);
  color: #fff;
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

.actions a {
  color: var(--cal-color-accent);
  text-decoration: none;
}

.link-danger {
  background: none;
  border: 0;
  color: var(--cal-color-danger);
  cursor: pointer;
  font: inherit;
  padding: 0;
}

.empty {
  color: var(--cal-color-muted);
}

.error {
  color: var(--cal-color-danger);
}
</style>
