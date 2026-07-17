<template>
  <div class="shell">
    <aside class="sidebar">
      <div class="brand">Callora</div>
      <nav>
        <RouterLink to="/">Übersicht</RouterLink>
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
import { onMounted } from 'vue'
import { RouterLink, RouterView, useRouter } from 'vue-router'
import { useAuthStore } from '@/core/auth/authStore'
import UserMenu from '@/core/ui/UserMenu.vue'

const store = useAuthStore()
const ctx = store.context
const router = useRouter()

// On a hard reload the in-memory context is empty; rehydrate it from the
// cookie session, or bounce to login if the session is gone.
onMounted(async () => {
  if (!ctx.value) {
    const ok = await store.loadContext()
    if (!ok) {
      router.push('/login')
    }
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
  justify-content: flex-end;
  padding: var(--cal-space) calc(var(--cal-space) * 2);
  border-bottom: 1px solid var(--cal-color-surface);
}
</style>
