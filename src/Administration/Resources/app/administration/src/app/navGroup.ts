import type { Component } from 'vue'
import type { AreaId } from './area'

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
  /**
   * The areas this item belongs to. Most belong to exactly one — that is the point
   * of the level: an operator looking at the platform should not have to scroll past
   * media and flows to reach the tenants.
   *
   * A few genuinely belong to two. Configuration and jobs exist at both the host and
   * the workspace, and pretending otherwise would mean either hiding them from
   * somebody who has them or inventing a second page for the same thing.
   */
  readonly areas: readonly AreaId[]
}

/** A titled section of the sidebar. A null label renders the items untitled. */
export interface NavGroup {
  readonly id: NavGroupId
  readonly label: string | null
  readonly items: readonly NavItem[]
}
