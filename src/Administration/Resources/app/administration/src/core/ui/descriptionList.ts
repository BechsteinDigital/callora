/** One row of {@link CalDescriptionList}. */
export interface DescriptionItem {
  /** Label, and the name of the slot that may override the rendered value. */
  readonly term: string
  readonly value?: string | number | null
  /** Monospaced value — identifiers, keys, hashes. */
  readonly mono?: boolean
}
