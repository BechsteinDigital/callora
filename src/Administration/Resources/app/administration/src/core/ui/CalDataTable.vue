<template>
  <div class="cal-table">
    <CalAlert v-if="error" tone="danger" class="cal-table__error">{{ error }}</CalAlert>

    <div class="cal-table__scroll">
      <table class="cal-table__grid">
        <thead>
          <tr>
            <th
              v-for="column in visibleColumns"
              :key="column.key"
              :style="{ width: column.width }"
              :class="{ 'is-end': column.align === 'end' }"
              scope="col"
            >
              {{ column.label }}
            </th>
          </tr>
        </thead>

        <tbody v-if="loading">
          <tr v-for="row in skeletonRows" :key="`skeleton-${row}`" class="cal-table__skeleton-row">
            <td v-for="column in visibleColumns" :key="column.key">
              <CalSkeleton :width="column.key === visibleColumns[0]?.key ? '55%' : '75%'" />
            </td>
          </tr>
        </tbody>

        <tbody v-else-if="rows.length">
          <tr v-for="(row, index) in rows" :key="keyOf(row, index)">
            <td
              v-for="column in visibleColumns"
              :key="column.key"
              :class="{ 'is-end': column.align === 'end', 'is-mono': column.mono }"
            >
              <slot :name="`cell-${column.key}`" :row="row" :index="index">
                {{ fallbackValue(row, column.key) }}
              </slot>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <CalEmptyState
      v-if="!loading && !rows.length && !error"
      class="cal-table__empty"
      compact
      :title="emptyTitle"
      :description="emptyDescription"
      :icon="emptyIcon"
    >
      <template v-if="$slots['empty-action']" #action><slot name="empty-action" /></template>
    </CalEmptyState>
  </div>
</template>

<script setup lang="ts" generic="Row extends Record<string, unknown>">
import { computed, type Component } from 'vue'
import CalAlert from './CalAlert.vue'
import CalEmptyState from './CalEmptyState.vue'
import CalSkeleton from './CalSkeleton.vue'
import type { DataTableColumn } from './dataTable'

/**
 * The one table in the shell. It owns the four states a list can be in —
 * loading, failed, empty, populated — so no module has to re-implement them,
 * and every list looks and behaves the same.
 *
 * Cells default to `row[column.key]`; anything richer (badges, actions, links)
 * comes from a `#cell-<key>` slot.
 */
const props = withDefaults(
  defineProps<{
    columns: readonly DataTableColumn[]
    rows: readonly Row[]
    /** Property holding a stable identity, or a function deriving one. */
    rowKey?: keyof Row | ((row: Row) => string)
    loading?: boolean
    error?: string | null
    emptyTitle?: string
    emptyDescription?: string
    emptyIcon?: Component
    /** How many placeholder rows to show while loading. */
    skeletonRowCount?: number
  }>(),
  {
    loading: false,
    error: null,
    emptyTitle: 'Keine Einträge vorhanden.',
    skeletonRowCount: 4,
  },
)

// Columns a caller hid (usually because the operator lacks the read permission
// for that data) never reach the DOM, so headers and cells cannot drift apart.
const visibleColumns = computed(() => props.columns.filter((column) => !column.hidden))

const skeletonRows = computed(() => Array.from({ length: props.skeletonRowCount }, (_, i) => i))

function keyOf(row: Row, index: number): string | number {
  if (typeof props.rowKey === 'function') {
    return props.rowKey(row)
  }
  if (props.rowKey) {
    return String(row[props.rowKey])
  }
  return index
}

function fallbackValue(row: Row, key: string): string {
  const value = row[key]
  return value === null || value === undefined || value === '' ? '—' : String(value)
}
</script>

<style scoped lang="scss">
.cal-table {
  display: flex;
  flex-direction: column;
}

.cal-table__error {
  margin: var(--cal-space-4) var(--cal-space-5) 0;
}

.cal-table__scroll {
  overflow-x: auto;
}

.cal-table__grid {
  width: 100%;
  border-collapse: collapse;
  font-size: var(--cal-text-md);
}

.cal-table__grid th {
  position: sticky;
  top: 0;
  z-index: 1;
  padding: var(--cal-space-2) var(--cal-space-4);
  background: var(--cal-bg-subtle);
  border-bottom: 1px solid var(--cal-border);
  text-align: left;
  font-size: var(--cal-text-xs);
  font-weight: var(--cal-weight-semibold);
  text-transform: uppercase;
  letter-spacing: var(--cal-tracking-wide);
  color: var(--cal-text-muted);
  white-space: nowrap;
}

.cal-table__grid td {
  padding: var(--cal-space-3) var(--cal-space-4);
  border-bottom: 1px solid var(--cal-border-subtle);
  color: var(--cal-text-secondary);
  vertical-align: middle;
}

.cal-table__grid tbody tr:last-child td {
  border-bottom: 0;
}

.cal-table__grid tbody tr:hover:not(.cal-table__skeleton-row) td {
  background: var(--cal-surface-hover);
}

.cal-table__grid td:first-child {
  color: var(--cal-text);
  font-weight: var(--cal-weight-medium);
}

.cal-table__grid th.is-end,
.cal-table__grid td.is-end {
  text-align: right;
}

.cal-table__grid td.is-mono {
  font-family: var(--cal-font-mono);
  font-size: var(--cal-text-sm);
  color: var(--cal-text-muted);
}

.cal-table__skeleton-row td {
  padding-block: var(--cal-space-4);
}
</style>
