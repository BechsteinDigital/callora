<template>
  <header class="topbar">
    <button type="button" class="topbar__menu" aria-label="Navigation öffnen" @click="openMobile">
      <CalIcon :icon="Menu" size="sm" />
    </button>

    <nav class="topbar__crumbs" aria-label="Brotkrumen">
      <template v-for="(crumb, index) in crumbs" :key="crumb.to ?? crumb.label">
        <CalIcon v-if="index > 0" class="topbar__crumb-sep" :icon="ChevronRight" size="sm" />
        <RouterLink v-if="crumb.to" class="topbar__crumb" :to="crumb.to">{{ crumb.label }}</RouterLink>
        <span v-else class="topbar__crumb is-current" aria-current="page">{{ crumb.label }}</span>
      </template>
    </nav>

    <div class="topbar__right">
      <button type="button" class="topbar__search" aria-label="Suchen" @click="$emit('open-search')">
        <CalIcon :icon="Search" size="sm" />
        <span class="topbar__search-label">{{ t('admin.search', 'Suchen') }}</span>
        <kbd class="topbar__kbd">{{ shortcutHint }}</kbd>
      </button>

      <AreaSwitcher />

      <WorkspaceSwitcher />

      <button
        type="button"
        class="topbar__icon-button"
        :aria-label="resolved === 'dark' ? 'Zu hellem Design wechseln' : 'Zu dunklem Design wechseln'"
        @click="toggle"
      >
        <CalIcon :icon="resolved === 'dark' ? Sun : Moon" size="sm" />
      </button>

      <UserMenu :label="displayName" :subtitle="scopeLabel" />
    </div>
  </header>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { ChevronRight, Menu, Moon, Search, Sun } from 'lucide-vue-next'
import CalIcon from '@/core/ui/CalIcon.vue'
import UserMenu from '@/core/ui/UserMenu.vue'
import AreaSwitcher from './AreaSwitcher.vue'
import { currentAreaSubject } from './area'
import { t } from '@/core/i18n/i18n'
import WorkspaceSwitcher from '@/core/workspace/WorkspaceSwitcher.vue'
import { useAuthStore } from '@/core/auth/authStore'
import { useTheme } from '@/core/design/theme'
import { useSidebar } from './sidebarState'
import { breadcrumbsFor } from './breadcrumbs'

defineEmits<{ 'open-search': [] }>()

const route = useRoute()
const ctx = useAuthStore().context
const { resolved, toggle } = useTheme()
const { openMobile } = useSidebar()

const crumbs = computed(() => breadcrumbsFor(route))

const displayName = computed(() => ctx.value?.displayName ?? ctx.value?.userId ?? 'Konto')

// Says *what kind of* session this is — operator or an admin bound to one
// workspace. It is the fastest answer to "why can I not see X?".
const scopeLabel = computed(() => {
  if (!ctx.value) {
    return undefined
  }
  // Nicht mehr der rohe Scope als Rückfall: Bei einer Mandanten-Sitzung stand dort "tenant"
  // — der Name der Ebene statt des Namens des Mandanten, in dem man sitzt.
  return ctx.value.isOperator ? 'Operator' : (currentAreaSubject(ctx.value) ?? undefined)
})

// Mac users expect ⌘K, everyone else Ctrl+K — showing the wrong one is worse
// than showing none.
const shortcutHint = computed(() => {
  const platform = typeof navigator === 'undefined' ? '' : navigator.platform
  return /mac|iphone|ipad/i.test(platform) ? '⌘K' : 'Strg K'
})
</script>

<style scoped lang="scss">
.topbar {
  position: sticky;
  top: 0;
  z-index: var(--cal-z-sticky);
  display: flex;
  align-items: center;
  gap: var(--cal-space-3);
  height: var(--cal-topbar-height);
  padding: 0 var(--cal-space-5);
  background: color-mix(in srgb, var(--cal-bg) 88%, transparent);
  backdrop-filter: blur(8px);
  border-bottom: 1px solid var(--cal-border-subtle);
}

.topbar__menu {
  display: none;
  padding: var(--cal-space-1);
  border: 0;
  border-radius: var(--cal-radius-sm);
  background: none;
  color: var(--cal-text-secondary);
  cursor: pointer;
}

.topbar__crumbs {
  display: flex;
  align-items: center;
  gap: var(--cal-space-1);
  flex: 1;
  min-width: 0;
  overflow: hidden;
}

.topbar__crumb {
  font-size: var(--cal-text-md);
  color: var(--cal-text-muted);
  white-space: nowrap;
}

.topbar__crumb:hover {
  color: var(--cal-text);
  text-decoration: none;
}

.topbar__crumb.is-current {
  color: var(--cal-text);
  font-weight: var(--cal-weight-medium);
  overflow: hidden;
  text-overflow: ellipsis;
}

.topbar__crumb-sep {
  color: var(--cal-text-muted);
  opacity: 0.6;
}

.topbar__right {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
  flex: none;
}

.topbar__search {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
  height: 28px;
  padding: 0 var(--cal-space-2);
  border: 1px solid var(--cal-border);
  border-radius: var(--cal-radius-sm);
  background: var(--cal-surface-inset);
  color: var(--cal-text-muted);
  font-size: var(--cal-text-md);
  cursor: pointer;
  transition: border-color var(--cal-duration-fast) var(--cal-ease);
}

.topbar__search:hover {
  border-color: var(--cal-border-strong);
  color: var(--cal-text-secondary);
}

.topbar__search-label {
  min-width: 88px;
  text-align: left;
}

.topbar__kbd {
  padding: 1px var(--cal-space-1);
  border: 1px solid var(--cal-border);
  border-radius: var(--cal-radius-xs);
  font-family: var(--cal-font);
  font-size: var(--cal-text-xs);
}

.topbar__icon-button {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border: 1px solid transparent;
  border-radius: var(--cal-radius-sm);
  background: none;
  color: var(--cal-text-secondary);
  cursor: pointer;
  transition:
    background var(--cal-duration-fast) var(--cal-ease),
    color var(--cal-duration-fast) var(--cal-ease);
}

.topbar__icon-button:hover {
  background: var(--cal-surface-hover);
  color: var(--cal-text);
}

@media (width <= 900px) {
  .topbar {
    padding: 0 var(--cal-space-4);
  }

  .topbar__menu {
    display: flex;
  }

  .topbar__search-label,
  .topbar__kbd {
    display: none;
  }

  .topbar__search {
    width: 28px;
    justify-content: center;
    padding: 0;
  }
}
</style>
