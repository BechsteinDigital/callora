import type { Component } from 'vue'

/**
 * Reiter, die eine App an ihrer eigenen Fläche beisteuert.
 *
 * Getrennt von der Slot-Registry, weil ein Reiter mehr ist als eine Komponente: Er trägt eine
 * Beschriftung und eine Identität, und er erscheint nur an der Fläche, der seine App zugewiesen
 * ist. Über den Slot-Mechanismus ginge nur das Erste — die Beschriftung müsste eine Konvention
 * im Slot-Namen sein, und ein Reiter erschiene an jeder Fläche.
 *
 * Die Zuordnung über die App-Zuweisung ist der Punkt: Ein Videokonferenz-Plugin führt seine
 * Räume dort, wo seine Fläche steht — und ein Betreiber, der die Fläche gestaltet, hat sie
 * daneben statt in einem anderen Menü.
 */
export interface SurfaceTabRegistration {
  /** Identität und Slot-Name des Reiterinhalts. */
  readonly id: string
  readonly label: string
  readonly component: Component
  /** Aufsteigend; gleiche Werte behalten die Registrierungsreihenfolge. */
  readonly order: number
  /**
   * Die App, zu der dieser Reiter gehört — vom Loader gesetzt.
   *
   * Er erscheint nur an Flächen, denen genau diese App zugewiesen ist. Ohne diese Bindung
   * bekäme jede Fläche jeden Reiter, und die Detailansicht wäre nach dem dritten Plugin
   * unbenutzbar. Das ist der Grund, warum Shopware Apps nur definierte Slots gibt.
   */
  readonly pluginId: string | null
}

const registrations: SurfaceTabRegistration[] = []

export function registerSurfaceTab(
  id: string,
  label: string,
  component: Component,
  order = 0,
  pluginId: string | null = null,
): void {
  registrations.push({ id, label, component, order, pluginId })
}

/**
 * Die Reiter, die an einer Fläche mit dieser App erscheinen.
 *
 * Ohne zugewiesene App keine Reiter: Eine Inhaltsfläche gehört niemandem, und ein Reiter ohne
 * Besitzer hätte nichts zu zeigen.
 */
export function surfaceTabsFor(appPluginId: string | null): SurfaceTabRegistration[] {
  if (!appPluginId) {
    return []
  }

  return registrations
    .filter((tab) => tab.pluginId === appPluginId)
    .sort((a, b) => a.order - b.order)
}

/** Test-/Hot-Reload-Hilfe — leert alle Registrierungen. */
export function resetSurfaceTabs(): void {
  registrations.length = 0
}
