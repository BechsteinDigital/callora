import type { Component } from 'vue'

/** One tab of {@link CalTabs}. Separate module so views can type their tab list. */
export interface TabItem {
  /** Identity of the tab and the name of the slot rendering its panel. */
  readonly value: string
  readonly label: string
  readonly icon?: Component
  /** Optional figure shown next to the label, e.g. the number of members. */
  readonly count?: number
}
