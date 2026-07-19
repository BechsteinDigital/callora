<template>
  <div class="shell">
    <aside class="sidebar">
      <div class="brand">Callora</div>
      <nav>
        <RouterLink to="/">Übersicht</RouterLink>
        <RouterLink to="/users">Benutzer</RouterLink>
        <RouterLink to="/roles">Rollen</RouterLink>
        <RouterLink to="/workspaces">Workspaces</RouterLink>
        <RouterLink v-if="canManageTenants" to="/tenants">Mandanten</RouterLink>
        <RouterLink to="/plugins">Plugins</RouterLink>
        <RouterLink to="/media">Medien</RouterLink>
        <RouterLink to="/config">Konfiguration</RouterLink>
      </nav>
    </aside>
    <div class="main">
      <header class="topbar">
        <UserMenu :label="ctx?.displayName ?? ctx?.userId ?? 'Konto'" />
      </header>
      <main class="content">
        <RouterView />
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, RouterView } from 'vue-router'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import UserMenu from '@/core/ui/UserMenu.vue'

// Context rehydration on a hard reload is handled by the route guard
// (authGuard), which runs and awaits /api/admin/context before this mounts.
const ctx = useAuthStore().context

// Tenant management is a feature-gated operator concern (BackendHostOptions
// .EnableTenantManagementApi); surface the nav entry only to callers who can
// actually read tenants. Hiding it is convenience, not a security boundary —
// the API stays permission-gated server-side (ADR-014 §3.4).
const canManageTenants = computed(() => hasPermission(ctx.value, 'tenant.read'))
</script>

<style scoped lang="scss">
.shell {
  display: grid;
  grid-template-columns: 220px 1fr;
  min-height: 100vh;
}

.sidebar {
  background: var(--cal-color-surface);
  padding: calc(var(--cal-space) * 2);
}

.brand {
  font-weight: 700;
  margin-bottom: calc(var(--cal-space) * 2);
}

.sidebar nav a {
  color: var(--cal-color-text);
  text-decoration: none;
  display: block;
  padding: var(--cal-space) 0;
}

.topbar {
  display: flex;
  justify-content: flex-end;
  padding: var(--cal-space) calc(var(--cal-space) * 2);
  border-bottom: 1px solid var(--cal-color-surface);
}
</style>
