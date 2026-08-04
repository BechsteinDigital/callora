import { computed, ref, type ComputedRef, type Ref } from 'vue'

/** What the operator chose: an explicit mode, or "whatever the system says". */
export type ThemePreference = 'system' | 'light' | 'dark'

/** The colour scheme actually rendered once the system signal is resolved. */
export type ResolvedTheme = 'light' | 'dark'

export const THEME_STORAGE_KEY = 'callora.admin.theme'

const SYSTEM_DARK_QUERY = '(prefers-color-scheme: dark)'

// Module singleton, like the auth and workspace stores: every consumer reads the
// same preference, and the <html> attribute is written from one place.
const preference = ref<ThemePreference>('system')
const systemPrefersDark = ref(true)

function readStored(): ThemePreference | null {
  try {
    const value = localStorage.getItem(THEME_STORAGE_KEY)
    return value === 'light' || value === 'dark' || value === 'system' ? value : null
  } catch {
    return null
  }
}

function writeStored(value: ThemePreference): void {
  try {
    localStorage.setItem(THEME_STORAGE_KEY, value)
  } catch {
    // Private mode / disabled storage: the choice still applies for this session.
  }
}

// The tokens read `data-theme` on <html>. "system" deliberately removes the
// attribute so the `prefers-color-scheme` media query in tokens.scss governs.
function applyToDocument(value: ThemePreference): void {
  const root = document.documentElement
  if (value === 'system') {
    root.removeAttribute('data-theme')
    return
  }
  root.setAttribute('data-theme', value)
}

/**
 * Applies the persisted preference and starts following the system signal.
 * Call once during bootstrap, before the app mounts, so the first paint is
 * already in the right colour scheme.
 */
export function initTheme(): void {
  preference.value = readStored() ?? 'system'
  applyToDocument(preference.value)

  if (typeof matchMedia !== 'function') {
    return
  }
  const mediaQuery = matchMedia(SYSTEM_DARK_QUERY)
  systemPrefersDark.value = mediaQuery.matches
  mediaQuery.addEventListener('change', (event) => {
    systemPrefersDark.value = event.matches
  })
}

export function useTheme(): {
  preference: Ref<ThemePreference>
  resolved: ComputedRef<ResolvedTheme>
  setPreference: (value: ThemePreference) => void
  toggle: () => void
} {
  const resolved = computed<ResolvedTheme>(() => {
    if (preference.value !== 'system') {
      return preference.value
    }
    return systemPrefersDark.value ? 'dark' : 'light'
  })

  function setPreference(value: ThemePreference): void {
    preference.value = value
    applyToDocument(value)
    writeStored(value)
  }

  // Flips to the opposite of what is currently on screen — which pins the choice.
  // Following the system again is an explicit act (the theme menu), not a step
  // in this rotation.
  function toggle(): void {
    setPreference(resolved.value === 'dark' ? 'light' : 'dark')
  }

  return { preference, resolved, setPreference, toggle }
}

/** Resets the module singleton — for tests only. */
export function resetTheme(): void {
  preference.value = 'system'
  systemPrefersDark.value = true
}
