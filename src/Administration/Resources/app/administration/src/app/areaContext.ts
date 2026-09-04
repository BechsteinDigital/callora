import { computed, ref } from 'vue'
import { useAuthStore } from '@/core/auth/authStore'
import { AREA_LABELS, currentArea, type AreaId } from './area'
import { availableAreas } from './navigation'

// Welchen Bereich die Shell gerade zeigt. Modul-Singleton wie der Workspace-Kontext,
// aus demselben Grund: Sidebar, Topbar und jede Seite müssen dieselbe Antwort lesen.
//
// Für alle außer Operatoren ist das keine Wahl, sondern eine Ablesung — der Bereich steht
// im Token. Ein Operator darf wählen, weil er tatsächlich alle drei erreicht; seine Wahl
// überlebt einen Reload, damit der Bereichswechsel nicht bei jedem Laden zurückspringt.
const STORAGE_KEY = 'callora.activeArea'

const chosen = ref<AreaId | null>(readStored())

function readStored(): AreaId | null {
  try {
    const value = localStorage.getItem(STORAGE_KEY)
    return value === 'platform' || value === 'tenant' || value === 'workspace' ? value : null
  } catch {
    return null
  }
}

export function useAreaContext() {
  const auth = useAuthStore()

  const areas = computed(() => availableAreas(auth.context.value))

  // Die Wahl gilt nur, solange sie erreichbar ist. Eine gespeicherte Wahl aus einer
  // früheren Sitzung überlebt die Anmeldung sonst als Bereich, den es für diesen Menschen
  // nicht gibt — und die Sidebar wäre leer, ohne dass irgendwo etwas fehlschlägt.
  const active = computed<AreaId | null>(() => {
    const reachable = areas.value
    if (chosen.value && reachable.includes(chosen.value)) {
      return chosen.value
    }
    return currentArea(auth.context.value) ?? reachable[0] ?? null
  })

  const label = computed(() => (active.value ? AREA_LABELS[active.value] : null))

  function setActive(area: AreaId): void {
    chosen.value = area
    try {
      localStorage.setItem(STORAGE_KEY, area)
    } catch {
      // Privater Modus: Die Auswahl gilt für diese Sitzung, nur nicht darüber hinaus.
    }
  }

  return { areas, active, label, setActive }
}

/** Nur für Tests: setzt die gespeicherte Wahl zurück. */
export function resetAreaContext(): void {
  chosen.value = null
}
