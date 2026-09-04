<template>
  <aside class="sidebar" :class="{ 'is-collapsed': collapsed, 'is-mobile-open': mobileOpen }">
    <div class="sidebar__brand">
      <RouterLink class="sidebar__brand-link" to="/" :title="collapsed ? 'Callora' : undefined">
        <span class="sidebar__mark" aria-hidden="true">C</span>
        <span v-if="!collapsed" class="sidebar__wordmark">Callora</span>
      </RouterLink>
      <button type="button" class="sidebar__mobile-close" aria-label="Navigation schließen" @click="closeMobile">
        <CalIcon :icon="X" size="sm" />
      </button>
    </div>

    <nav class="sidebar__nav" aria-label="Hauptnavigation">
      <div v-for="group in groups" :key="group.id" class="sidebar__group">
        <p v-if="group.label && !collapsed" class="sidebar__group-label">{{ group.label }}</p>
        <div v-else-if="group.label" class="sidebar__group-rule" aria-hidden="true" />
        <RouterLink
          v-for="item in group.items"
          :key="item.to"
          class="sidebar__link"
          :class="{ 'is-active': isNavItemActive(item.to, route.path) }"
          :to="item.to"
          :title="collapsed ? item.label : undefined"
          @click="closeMobile"
        >
          <CalIcon class="sidebar__link-icon" :icon="item.icon" size="sm" />
          <span v-if="!collapsed" class="sidebar__link-label">{{ item.label }}</span>
        </RouterLink>
      </div>

      <!-- Die Flächen stehen als eigener Bereich, nicht als Menüpunkt: Eine Fläche ist ein
           ORT, an dem gearbeitet wird — die Entsprechung zu Shopwares Verkaufskanälen. Wer
           eine Seite bearbeiten will, sucht die Fläche, nicht den Menüpunkt „Flächen". -->
      <div v-if="surfaceRoots.length" class="sidebar__group">
        <div class="sidebar__group-head">
          <p v-if="!collapsed" class="sidebar__group-label">Flächen</p>
          <div v-else class="sidebar__group-rule" aria-hidden="true" />
          <RouterLink
            v-if="!collapsed"
            class="sidebar__group-action"
            to="/surfaces"
            title="Flächen verwalten"
            @click="closeMobile"
          >
            <CalIcon :icon="Settings2" size="sm" />
          </RouterLink>
        </div>
        <RouterLink
          v-for="surface in surfaceRoots"
          :key="surface.surfaceKey"
          class="sidebar__link"
          :class="{ 'is-active': route.path === `/surfaces/${surface.surfaceKey}` }"
          :to="`/surfaces/${encodeURIComponent(surface.surfaceKey)}`"
          :title="collapsed ? surface.displayName : undefined"
          @click="closeMobile"
        >
          <CalIcon
            class="sidebar__link-icon"
            :icon="surface.routing === 'Application' ? AppWindow : Store"
            size="sm"
          />
          <span v-if="!collapsed" class="sidebar__link-label">
            {{ surface.displayName || surface.surfaceKey }}
          </span>
        </RouterLink>
      </div>

      <div v-if="pluginNav.length" class="sidebar__group">
        <p v-if="!collapsed" class="sidebar__group-label">Erweiterungen</p>
        <div v-else class="sidebar__group-rule" aria-hidden="true" />
        <RouterLink
          v-for="item in pluginNav"
          :key="`${item.pluginId}:${item.id}`"
          class="sidebar__link"
          :class="{ 'is-active': isNavItemActive(item.to, route.path) }"
          :to="item.to"
          :title="collapsed ? item.label : undefined"
          @click="closeMobile"
        >
          <CalIcon class="sidebar__link-icon" :icon="resolvePluginIcon(item.icon)" size="sm" />
          <span v-if="!collapsed" class="sidebar__link-label">{{ item.label }}</span>
        </RouterLink>
      </div>
    </nav>

    <button
      type="button"
      class="sidebar__collapse"
      :aria-label="collapsed ? 'Navigation ausklappen' : 'Navigation einklappen'"
      :aria-expanded="!collapsed"
      @click="toggleCollapsed"
    >
      <CalIcon :icon="collapsed ? PanelLeftOpen : PanelLeftClose" size="sm" />
      <span v-if="!collapsed">Einklappen</span>
    </button>
  </aside>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { AppWindow, PanelLeftClose, PanelLeftOpen, Settings2, Store, X } from 'lucide-vue-next'
import CalIcon from '@/core/ui/CalIcon.vue'
import { useAuthStore } from '@/core/auth/authStore'
import { useAreaContext } from './areaContext'
import { visibleNavGroups } from './navigation'
import { isNavItemActive } from './navActive'
import { useSidebar } from './sidebarState'
import { usePluginNavigation } from '@/core/extensions/pluginNavigation'
import { useSurfaceNavigation } from '@/core/workspace/surfaceNavigation'
import { resolvePluginIcon } from '@/core/extensions/pluginIcons'

const route = useRoute()
const ctx = useAuthStore().context
const { active: activeArea } = useAreaContext()
const groups = computed(() => visibleNavGroups(ctx.value, activeArea.value))

// Plugin-contributed entries get their own group at the bottom. The server
// already permission-filters them, so they render as delivered.
const { items: pluginNav } = usePluginNavigation()

const { collapsed, mobileOpen, toggleCollapsed, closeMobile } = useSidebar()

// Die Wurzelflächen des aktiven Workspaces. Geteilt mit der Flächenansicht, damit eine neu
// angelegte Seite nicht an einer Stelle auftaucht und an der anderen fehlt.
const { roots: surfaceRoots } = useSurfaceNavigation()
</script>

<style scoped lang="scss">
.sidebar {
  display: flex;
  flex-direction: column;
  width: var(--cal-sidebar-width);
  height: 100vh;
  position: sticky;
  top: 0;
  background: var(--cal-bg-subtle);
  border-right: 1px solid var(--cal-border-subtle);
  transition: width var(--cal-duration-base) var(--cal-ease);
}

.sidebar.is-collapsed {
  width: var(--cal-sidebar-width-collapsed);
}

/* ---------------------------------------------------------------- Marke */
.sidebar__brand {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: var(--cal-topbar-height);
  padding: 0 var(--cal-space-3);
  flex: none;
}

.sidebar__brand-link {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
  color: var(--cal-text);
  min-width: 0;
}

.sidebar__brand-link:hover {
  text-decoration: none;
}

.sidebar__mark {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  flex: none;
  border-radius: var(--cal-radius-sm);
  background: var(--cal-accent);
  color: var(--cal-accent-contrast);
  font-size: var(--cal-text-sm);
  font-weight: var(--cal-weight-bold);
}

.sidebar__wordmark {
  font-size: var(--cal-text-base);
  font-weight: var(--cal-weight-semibold);
  letter-spacing: -0.01em;
  white-space: nowrap;
}

.sidebar__mobile-close {
  display: none;
  padding: var(--cal-space-1);
  border: 0;
  background: none;
  color: var(--cal-text-muted);
  cursor: pointer;
}

/* ------------------------------------------------------------ Navigation */
.sidebar__nav {
  flex: 1;
  overflow-y: auto;
  padding: var(--cal-space-2);
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-4);
}

.sidebar__group {
  display: flex;
  flex-direction: column;
  gap: 1px;
}

.sidebar__group-label {
  padding: var(--cal-space-1) var(--cal-space-2) var(--cal-space-2);
  font-size: var(--cal-text-xs);
  font-weight: var(--cal-weight-semibold);
  text-transform: uppercase;
  letter-spacing: var(--cal-tracking-wide);
  color: var(--cal-text-muted);
}

.sidebar__group-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.sidebar__group-action {
  display: flex;
  padding: var(--cal-space-1) var(--cal-space-2);
  color: var(--cal-text-muted);
}

.sidebar__group-action:hover {
  color: var(--cal-text);
  text-decoration: none;
}

/* Collapsed, a group heading would not fit — a hairline keeps the rhythm. */
.sidebar__group-rule {
  height: 1px;
  margin: var(--cal-space-2) var(--cal-space-2) var(--cal-space-3);
  background: var(--cal-border-subtle);
}

.sidebar__link {
  display: flex;
  align-items: center;
  gap: var(--cal-space-3);
  height: 32px;
  padding: 0 var(--cal-space-2);
  border-radius: var(--cal-radius-sm);
  color: var(--cal-text-secondary);
  font-size: var(--cal-text-md);
  font-weight: var(--cal-weight-medium);
  white-space: nowrap;
  transition:
    background var(--cal-duration-fast) var(--cal-ease),
    color var(--cal-duration-fast) var(--cal-ease);
}

.sidebar__link:hover {
  background: var(--cal-surface-hover);
  color: var(--cal-text);
  text-decoration: none;
}

.sidebar__link-icon {
  color: var(--cal-text-muted);
  transition: color var(--cal-duration-fast) var(--cal-ease);
}

.sidebar__link:hover .sidebar__link-icon {
  color: var(--cal-text-secondary);
}

/* The active state comes from isNavItemActive, not from router-link-active:
   the latter matches by prefix and would light the dashboard on every route. */
.sidebar__link.is-active {
  background: var(--cal-accent-subtle);
  color: var(--cal-accent);
}

.sidebar__link.is-active .sidebar__link-icon {
  color: var(--cal-accent);
}

.sidebar__link-label {
  overflow: hidden;
  text-overflow: ellipsis;
}

.sidebar.is-collapsed .sidebar__link {
  justify-content: center;
  padding: 0;
}

/* ------------------------------------------------------------- Einklappen */
.sidebar__collapse {
  display: flex;
  align-items: center;
  gap: var(--cal-space-3);
  height: 32px;
  margin: var(--cal-space-2);
  padding: 0 var(--cal-space-2);
  border: 0;
  border-radius: var(--cal-radius-sm);
  background: none;
  color: var(--cal-text-muted);
  font-size: var(--cal-text-md);
  cursor: pointer;
  flex: none;
}

.sidebar__collapse:hover {
  background: var(--cal-surface-hover);
  color: var(--cal-text);
}

.sidebar.is-collapsed .sidebar__collapse {
  justify-content: center;
  padding: 0;
}

/* ----------------------------------------------------------------- Mobil */
@media (width <= 900px) {
  .sidebar {
    position: fixed;
    top: 0;
    left: 0;
    z-index: var(--cal-z-modal);
    width: var(--cal-sidebar-width);
    transform: translateX(-100%);
    transition: transform var(--cal-duration-base) var(--cal-ease-out);
    box-shadow: var(--cal-shadow-xl);
  }

  .sidebar.is-mobile-open {
    transform: translateX(0);
  }

  /* Collapsing is a desktop affordance; on mobile the drawer is the answer. */
  .sidebar.is-collapsed {
    width: var(--cal-sidebar-width);
  }

  .sidebar.is-collapsed .sidebar__link {
    justify-content: flex-start;
    padding: 0 var(--cal-space-2);
  }

  .sidebar__mobile-close {
    display: flex;
  }

  .sidebar__collapse {
    display: none;
  }
}
</style>
