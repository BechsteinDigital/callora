<template>
  <section class="roles">
    <header class="head">
      <h1>Rollen</h1>
      <div class="head-actions">
        <ExtensionSlot name="roles.list.toolbar" />
        <RouterLink v-if="canManage" class="new" to="/roles/new">Neu anlegen</RouterLink>
      </div>
    </header>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-else-if="loading">Lädt…</p>

    <table v-else class="grid">
      <thead>
        <tr>
          <th>Rolle</th>
          <th>Rechte</th>
          <th></th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="r in roles" :key="r.role">
          <td>
            {{ r.role }}
            <span v-if="r.role === SYSTEM_ROLE" class="badge">System</span>
          </td>
          <td>{{ describePermissions(r) }}</td>
          <td class="actions">
            <template v-if="canManage && r.role !== SYSTEM_ROLE">
              <RouterLink :to="`/roles/${r.role}`">Bearbeiten</RouterLink>
              <button type="button" class="link-danger" @click="remove(r)">Löschen</button>
            </template>
          </td>
        </tr>
        <tr v-if="!roles.length">
          <td colspan="3" class="empty">Keine Rollen vorhanden.</td>
        </tr>
      </tbody>
    </table>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { rolesApi, SYSTEM_ROLE, type Role } from './rolesApi'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'

const roles = ref<Role[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const ctx = useAuthStore().context
const canManage = computed(() => hasPermission(ctx.value, 'role.update'))

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
    roles.value = await rolesApi.list()
  } catch (e) {
    error.value = (e as Error).message
  } finally {
    loading.value = false
  }
}

async function remove(role: Role): Promise<void> {
  if (!window.confirm(`Rolle „${role.role}“ löschen?`)) {
    return
  }
  try {
    await rolesApi.remove(role.role)
    await load()
  } catch (e) {
    error.value = (e as Error).message
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.roles {
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

.badge {
  font-size: 0.75em;
  color: var(--cal-color-muted);
  border: 1px solid var(--cal-color-muted);
  border-radius: var(--cal-radius);
  padding: 0 calc(var(--cal-space) * 0.75);
  margin-left: var(--cal-space);
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
