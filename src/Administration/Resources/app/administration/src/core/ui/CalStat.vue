<template>
  <article class="cal-stat" :class="{ 'is-linked': !!to }">
    <component :is="to ? RouterLink : 'div'" :to="to" class="cal-stat__inner">
      <div class="cal-stat__head">
        <span class="cal-stat__label">{{ label }}</span>
        <span v-if="icon" class="cal-stat__icon"><CalIcon :icon="icon" size="sm" /></span>
      </div>
      <CalSkeleton v-if="loading" class="cal-stat__loading" width="42%" height="26px" />
      <span v-else class="cal-stat__value" :class="{ 'is-unavailable': unavailable }">{{ display }}</span>
      <span v-if="caption" class="cal-stat__caption">{{ caption }}</span>
    </component>
  </article>
</template>

<script setup lang="ts">
import { computed, type Component } from 'vue'
import { RouterLink } from 'vue-router'
import CalIcon from './CalIcon.vue'
import CalSkeleton from './CalSkeleton.vue'

/**
 * A single headline figure on the dashboard. It distinguishes "still loading"
 * from "could not be read" — an operator must never mistake a failed metric for
 * a genuine zero.
 */
const props = defineProps<{
  label: string
  value?: number | string | null
  caption?: string
  icon?: Component
  loading?: boolean
  /** The value could not be read; renders a dash in muted colour. */
  unavailable?: boolean
  /** Makes the whole tile a link to the list behind the figure. */
  to?: string
}>()

const display = computed(() => (props.unavailable ? '—' : (props.value ?? '—')))
</script>

<style scoped lang="scss">
.cal-stat {
  background: var(--cal-surface);
  border: 1px solid var(--cal-border);
  border-radius: var(--cal-radius-lg);
  transition:
    border-color var(--cal-duration-fast) var(--cal-ease),
    background var(--cal-duration-fast) var(--cal-ease);
}

.cal-stat.is-linked:hover {
  border-color: var(--cal-border-strong);
  background: var(--cal-surface-raised);
}

.cal-stat__inner {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-2);
  padding: var(--cal-space-4) var(--cal-space-5);
  color: inherit;
  text-decoration: none;
}

.cal-stat__inner:hover {
  text-decoration: none;
}

.cal-stat__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--cal-space-2);
}

.cal-stat__label {
  font-size: var(--cal-text-md);
  color: var(--cal-text-secondary);
}

.cal-stat__icon {
  color: var(--cal-text-muted);
}

.cal-stat__value {
  font-size: var(--cal-text-3xl);
  font-weight: var(--cal-weight-semibold);
  line-height: var(--cal-leading-tight);
  letter-spacing: -0.02em;
  font-variant-numeric: tabular-nums;
  color: var(--cal-text);
}

.cal-stat__value.is-unavailable {
  color: var(--cal-text-muted);
}

.cal-stat__loading {
  margin: var(--cal-space-1) 0;
}

.cal-stat__caption {
  font-size: var(--cal-text-sm);
  color: var(--cal-text-muted);
}
</style>
