import type { Component } from 'vue'

// UI extension registry: plugins register components against a named slot, and
// core views render them via <ExtensionSlot>. This is the Vue-3 alternative to
// Shopware-style component overrides — additive slots, no internal-structure
// coupling. Slot IDs follow "{module}.{view}.{position}" and are a public
// contract; keep them stable.
export interface ExtensionRegistration {
  readonly slot: string
  readonly component: Component
  // Ascending; ties keep registration order. Mirrors the plugin navigation Order.
  readonly order: number
}

const registrations: ExtensionRegistration[] = []

export function registerExtension(slot: string, component: Component, order = 0): void {
  registrations.push({ slot, component, order })
}

// Returns the components registered for a slot, ordered. Stable: equal orders
// preserve registration order (Array.prototype.sort is stable in modern engines).
export function getExtensions(slot: string): Component[] {
  return registrations
    .filter((r) => r.slot === slot)
    .sort((a, b) => a.order - b.order)
    .map((r) => r.component)
}

// Test/hot-reload aid — clears all registrations.
export function resetExtensions(): void {
  registrations.length = 0
}
