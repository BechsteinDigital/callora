import type { Component } from 'vue'

/** One entry of the command palette: a place to go, or an action to run. */
export interface CommandItem {
  readonly id: string
  readonly label: string
  /** Group heading in the result list, e.g. "Verwaltung" or "Aktionen". */
  readonly section?: string
  readonly icon?: Component
  /** Route to navigate to. Mutually exclusive with `run`. */
  readonly to?: string
  /** Action to perform. Mutually exclusive with `to`. */
  readonly run?: () => void
  /**
   * Extra terms this entry should be findable by — synonyms, English names, the
   * words an operator is likely to type ("logout" for "Abmelden").
   */
  readonly keywords?: readonly string[]
}
