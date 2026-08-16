import type { RouteLocationNormalizedLoaded } from 'vue-router'

declare module 'vue-router' {
  interface RouteMeta {
    /**
     * Nur für Ansichten, die einen Parameterwechsel selbst verarbeiten und ihren Zustand
     * dabei behalten sollen: derselbe Wert auf allen Pfaden, die zu ihnen führen. Ohne ihn
     * bekommt jeder Pfad eine eigene Instanz — der Default, siehe `AppRouterView`.
     */
    viewKey?: string
  }
}

/**
 * Der Key, unter dem `AppRouterView` die aktive Ansicht rendert. Standard ist der Pfad,
 * damit zwei Pfade, die sich eine Komponente teilen, sich keine Instanz teilen.
 */
export function viewKey(route: RouteLocationNormalizedLoaded): string {
  return route.meta.viewKey ?? route.path
}
