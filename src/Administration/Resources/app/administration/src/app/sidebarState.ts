import { ref, type Ref } from 'vue'

/**
 * Whether the sidebar is reduced to its icon rail, and whether the mobile
 * overlay is open.
 *
 * The collapsed choice persists: an operator who works on a narrow screen — or
 * simply prefers the extra width for tables — should not have to re-collapse it
 * on every visit. The mobile overlay deliberately does not persist; it is a
 * momentary state that must never survive a reload.
 */
const STORAGE_KEY = 'callora.admin.sidebarCollapsed'

const collapsed = ref(false)
const mobileOpen = ref(false)

function persist(value: boolean): void {
  try {
    localStorage.setItem(STORAGE_KEY, value ? '1' : '0')
  } catch {
    // Private mode / disabled storage: the choice still holds for this session.
  }
}

/** Restores the persisted collapse state. Call once during bootstrap. */
export function initSidebar(): void {
  try {
    collapsed.value = localStorage.getItem(STORAGE_KEY) === '1'
  } catch {
    collapsed.value = false
  }
}

export function useSidebar(): {
  collapsed: Ref<boolean>
  mobileOpen: Ref<boolean>
  toggleCollapsed: () => void
  openMobile: () => void
  closeMobile: () => void
} {
  function toggleCollapsed(): void {
    collapsed.value = !collapsed.value
    persist(collapsed.value)
  }

  return {
    collapsed,
    mobileOpen,
    toggleCollapsed,
    openMobile: () => {
      mobileOpen.value = true
    },
    closeMobile: () => {
      mobileOpen.value = false
    },
  }
}

/** Resets the module singleton — for tests only. */
export function resetSidebar(): void {
  collapsed.value = false
  mobileOpen.value = false
}
