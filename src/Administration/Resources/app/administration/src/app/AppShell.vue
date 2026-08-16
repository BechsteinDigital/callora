<template>
  <div class="shell" :class="{ 'is-collapsed': collapsed }">
    <AppSidebar />

    <div v-if="mobileOpen" class="shell__scrim" @click="closeMobile" />

    <div class="shell__main">
      <AppTopbar @open-search="searchOpen = true" />
      <main class="shell__content">
        <AppRouterView />
      </main>
    </div>

    <AppCommandPalette v-model:open="searchOpen" :commands="commands" />
    <CalConfirmHost />
    <CalToastViewport />
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppRouterView from './AppRouterView.vue'
import AppSidebar from './AppSidebar.vue'
import AppTopbar from './AppTopbar.vue'
import AppCommandPalette from './AppCommandPalette.vue'
import CalConfirmHost from '@/core/feedback/CalConfirmHost.vue'
import CalToastViewport from '@/core/feedback/CalToastViewport.vue'
import { useAuthStore } from '@/core/auth/authStore'
import { endSession } from '@/core/auth/session'
import { loadPluginNavigation } from '@/core/extensions/pluginNavigation'
import { useOnboarding, shouldAutoRedirect, markAutoShown } from '@/modules/onboarding/onboarding'
import { initSidebar, useSidebar } from './sidebarState'
import { useCommands } from './useCommands'

// Context rehydration on a hard reload is handled by the route guard
// (authGuard), which runs and awaits /api/admin/context before this mounts.
const authStore = useAuthStore()
const ctx = authStore.context

const router = useRouter()
const route = useRoute()

const { collapsed, mobileOpen, closeMobile } = useSidebar()
const searchOpen = ref(false)

async function logout(): Promise<void> {
  await authStore.logout()
  endSession()
  void router.push('/login')
}

const commands = useCommands(() => void logout())

// Cmd/Ctrl+K anywhere opens the palette; Escape closes it (Radix handles that).
// Bound on the shell rather than per view so it works on every route.
function onKeydown(event: KeyboardEvent): void {
  if (event.key.toLowerCase() === 'k' && (event.metaKey || event.ctrlKey)) {
    event.preventDefault()
    searchOpen.value = !searchOpen.value
  }
}

// Navigating away from a route always closes the mobile drawer — leaving it open
// over the new page is the classic drawer bug.
watch(() => route.fullPath, closeMobile)

onMounted(async () => {
  initSidebar()
  window.addEventListener('keydown', onKeydown)
  void loadPluginNavigation()

  // First-run onboarding: on a fresh install (operator, no workspace yet) send
  // the operator to the wizard once. Only operators; skipped on the wizard route
  // itself and once auto-shown (localStorage), so it never re-forces later.
  if (!ctx.value?.isOperator) {
    return
  }
  await useOnboarding().loadStatus()
  if (shouldAutoRedirect() && route.path !== '/onboarding') {
    markAutoShown()
    void router.push('/onboarding')
  }
})

onUnmounted(() => {
  window.removeEventListener('keydown', onKeydown)
})
</script>

<style scoped lang="scss">
.shell {
  display: grid;
  grid-template-columns: var(--cal-sidebar-width) minmax(0, 1fr);
  min-height: 100vh;
  transition: grid-template-columns var(--cal-duration-base) var(--cal-ease);
}

.shell.is-collapsed {
  grid-template-columns: var(--cal-sidebar-width-collapsed) minmax(0, 1fr);
}

.shell__main {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.shell__content {
  flex: 1;
  min-width: 0;
}

.shell__scrim {
  position: fixed;
  inset: 0;
  z-index: var(--cal-z-overlay);
  background: var(--cal-overlay-backdrop);
}

@media (width <= 900px) {
  .shell,
  .shell.is-collapsed {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
