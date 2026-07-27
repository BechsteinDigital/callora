<template>
  <div class="shell">
    <aside class="sidebar">
      <div class="brand">Callora</div>
      <nav>
        <RouterLink v-for="item in nav" :key="item.to" :to="item.to">{{ item.label }}</RouterLink>
      </nav>
    </aside>
    <div class="main">
      <header class="topbar">
        <WorkspaceSwitcher />
        <UserMenu :label="ctx?.displayName ?? ctx?.userId ?? 'Konto'" />
      </header>
      <main class="content">
        <RouterView />
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { RouterLink, RouterView, useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/core/auth/authStore'
import { visibleNavItems } from './navigation'
import { useOnboarding, shouldAutoRedirect, markAutoShown } from '@/modules/onboarding/onboarding'
import UserMenu from '@/core/ui/UserMenu.vue'
import WorkspaceSwitcher from '@/core/workspace/WorkspaceSwitcher.vue'

// Context rehydration on a hard reload is handled by the route guard
// (authGuard), which runs and awaits /api/admin/context before this mounts.
const ctx = useAuthStore().context

// The sidebar mirrors each target's server-side read gate; a scoped admin only
// sees what they may open. Hiding is convenience, not a security boundary.
const nav = computed(() => visibleNavItems(ctx.value))

// First-run onboarding: on a fresh install (operator, no workspace yet) send the
// operator to the wizard once. Only operators; skipped on the wizard route itself
// and once auto-shown (localStorage), so it never re-forces on later reloads.
const router = useRouter()
const route = useRoute()
onMounted(async () => {
  if (!ctx.value?.isOperator) {
    return
  }
  await useOnboarding().loadStatus()
  if (shouldAutoRedirect() && route.path !== '/onboarding') {
    markAutoShown()
    void router.push('/onboarding')
  }
})
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
  justify-content: space-between;
  align-items: center;
  gap: var(--cal-space);
  padding: var(--cal-space) calc(var(--cal-space) * 2);
  border-bottom: 1px solid var(--cal-color-surface);
}
</style>
