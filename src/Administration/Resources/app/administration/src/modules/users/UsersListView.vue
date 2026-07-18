<template>
  <section class="users">
    <header class="head">
      <h1>Benutzer</h1>
      <div class="head-actions">
        <ExtensionSlot name="users.list.toolbar" />
        <RouterLink v-if="canCreate" class="new" to="/users/new">Neu anlegen</RouterLink>
      </div>
    </header>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-else-if="loading">Lädt…</p>

    <table v-else class="grid">
      <thead>
        <tr>
          <th>Login</th>
          <th>E-Mail</th>
          <th>Name</th>
          <th v-if="canReadRoles">Rolle</th>
          <th>Passwort</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="u in users" :key="u.externalId">
          <td>{{ u.externalId }}</td>
          <td>{{ u.email ?? '—' }}</td>
          <td>{{ u.displayName ?? '—' }}</td>
          <td v-if="canReadRoles">{{ roleFor(u.externalId) }}</td>
          <td>{{ u.hasPassword ? '✓' : '—' }}</td>
          <td class="actions">
            <RouterLink v-if="canUpdate" :to="`/users/${u.externalId}`">Bearbeiten</RouterLink>
            <button v-if="canDelete" type="button" class="link-danger" @click="remove(u)">Löschen</button>
            <ExtensionSlot name="users.list.row-actions" :ctx="u" />
          </td>
        </tr>
        <tr v-if="!users.length">
          <td :colspan="columnCount" class="empty">Keine Benutzer vorhanden.</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { usersApi, type BackendUser } from './usersApi'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'
import { useService } from '@/core/extensions/services'
import { runHook } from '@/core/extensions/hooks'

const users = ref<BackendUser[]>([])
const roleAssignments = ref<Record<string, string>>({})
const loading = ref(true)
const error = ref<string | null>(null)

const ctx = useAuthStore().context
const canCreate = computed(() => hasPermission(ctx.value, 'user.create'))
const canUpdate = computed(() => hasPermission(ctx.value, 'user.update'))
const canDelete = computed(() => hasPermission(ctx.value, 'user.delete'))
const canReadRoles = computed(() => hasPermission(ctx.value, 'role.read'))
const columnCount = computed(() => (canReadRoles.value ? 6 : 5))

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
  const confirmed = window.confirm(
    `Benutzer „${user.externalId}“ löschen? Das anonymisiert auch den Audit-Trail (Art. 17).`,
  )
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
    await load()
  } catch (e) {
    error.value = (e as Error).message
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.users {
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

.actions {
  display: flex;
  gap: calc(var(--cal-space) * 1.5);
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
