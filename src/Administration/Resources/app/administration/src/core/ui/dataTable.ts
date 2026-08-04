/**
 * Column model of {@link CalDataTable}.
 *
 * A separate module because the SFC's generic `<script setup>` cannot export
 * types, and every list view needs to describe its columns as plain data.
 */
export interface DataTableColumn {
  /** Property read for the default cell, and the name of the `#cell-<key>` slot. */
  readonly key: string
  /** Column heading. */
  readonly label: string
  /** Fixed width, e.g. '160px' or '20%'. Omit to let the column size itself. */
  readonly width?: string
  /** Right-aligned columns are for numbers and trailing action groups. */
  readonly align?: 'start' | 'end'
  /** Monospaced cell — for identifiers, keys and hashes. */
  readonly mono?: boolean
  /**
   * Drops the column entirely. Views use this for data behind a read permission
   * the current operator lacks, so header and cells can never disagree.
   */
  readonly hidden?: boolean
}
