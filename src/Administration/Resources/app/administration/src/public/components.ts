/**
 * The shell's primitives, for a plugin to build its admin UI from.
 *
 * They style themselves entirely through `--cal-*` tokens, so a plugin page looks like the shell
 * without copying a single colour — and follows a theme change without being rebuilt. That is the
 * reason to use them over hand-rolled markup, more than the saved effort.
 *
 * A plugin running INSIDE the shell needs no stylesheet of its own: Vue derives the scoped-style
 * ids from the file paths, so the library build produces the same `data-v-*` attributes the shell
 * already carries styles for. `@callora/admin/style.css` exists for the other case — a Storybook,
 * an isolated test — where no shell has loaded them. A test in this directory pins that property,
 * because it depends on both builds seeing the same project root.
 *
 * `UserMenu` is deliberately absent: it is shell chrome, not a building block.
 */

export { default as CalAlert } from '@/core/ui/CalAlert.vue'
export { default as CalBadge } from '@/core/ui/CalBadge.vue'
export { default as CalButton } from '@/core/ui/CalButton.vue'
export { default as CalCard } from '@/core/ui/CalCard.vue'
export { default as CalCheckbox } from '@/core/ui/CalCheckbox.vue'
export { default as CalDataTable } from '@/core/ui/CalDataTable.vue'
export { default as CalDescriptionList } from '@/core/ui/CalDescriptionList.vue'
export { default as CalDialog } from '@/core/ui/CalDialog.vue'
export { default as CalEmptyState } from '@/core/ui/CalEmptyState.vue'
export { default as CalField } from '@/core/ui/CalField.vue'
export { default as CalIcon } from '@/core/ui/CalIcon.vue'
export { default as CalInput } from '@/core/ui/CalInput.vue'
export { default as CalPage } from '@/core/ui/CalPage.vue'
export { default as CalPageHeader } from '@/core/ui/CalPageHeader.vue'
export { default as CalSelect } from '@/core/ui/CalSelect.vue'
export { default as CalSkeleton } from '@/core/ui/CalSkeleton.vue'
export { default as CalSpinner } from '@/core/ui/CalSpinner.vue'
export { default as CalStat } from '@/core/ui/CalStat.vue'
export { default as CalSwitch } from '@/core/ui/CalSwitch.vue'
export { default as CalTabs } from '@/core/ui/CalTabs.vue'
export { default as CalTextarea } from '@/core/ui/CalTextarea.vue'

// The shapes those components take as props — a plugin building a table needs the column type.
export type { DataTableColumn } from '@/core/ui/dataTable'
export type { DescriptionItem } from '@/core/ui/descriptionList'
export type { TabItem } from '@/core/ui/tabs'
