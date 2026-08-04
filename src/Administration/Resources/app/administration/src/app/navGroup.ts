import type { Component } from 'vue'

/** The sections the sidebar is divided into, in display order. */
export type NavGroupId = 'overview' | 'management' | 'content' | 'system'

export interface NavItem {
  readonly label: string
  readonly to: string
  /** lucide component rendered in front of the label. */
  readonly icon: Component
  /**
   * The permission gating visibility. Absent = always visible. It mirrors the
   * server-side read gate of the target so the nav does not offer a link the API
   * would refuse — hiding is convenience, NOT a security boundary (the server
   * stays authoritative, ADR-014 §3.4).
   */
  readonly permission?: string
  readonly group: NavGroupId
}

/** A titled section of the sidebar. A null label renders the items untitled. */
export interface NavGroup {
  readonly id: NavGroupId
  readonly label: string | null
  readonly items: readonly NavItem[]
}
