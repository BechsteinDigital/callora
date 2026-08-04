<template>
  <span class="cal-badge" :class="[`is-${tone}`, `is-${variant}`]">
    <span v-if="dot" class="cal-badge__dot" />
    <slot />
  </span>
</template>

<script setup lang="ts">
/**
 * Status marker for tables and detail views. `tone` carries the meaning — views
 * map their domain state onto a tone rather than picking colours themselves, so
 * "active", "signed" and "healthy" read identically across modules.
 */
withDefaults(
  defineProps<{
    tone?: 'neutral' | 'accent' | 'success' | 'warning' | 'danger' | 'info'
    variant?: 'subtle' | 'outline'
    dot?: boolean
  }>(),
  { tone: 'neutral', variant: 'subtle', dot: false },
)
</script>

<style scoped lang="scss">
.cal-badge {
  display: inline-flex;
  align-items: center;
  gap: var(--cal-space-1);
  height: 20px;
  padding: 0 var(--cal-space-2);
  border: 1px solid transparent;
  border-radius: var(--cal-radius-full);
  font-size: var(--cal-text-xs);
  font-weight: var(--cal-weight-medium);
  line-height: 1;
  white-space: nowrap;
}

.cal-badge__dot {
  width: 5px;
  height: 5px;
  border-radius: var(--cal-radius-full);
  background: currentcolor;
}

.cal-badge.is-neutral {
  color: var(--cal-text-secondary);
}

.cal-badge.is-neutral.is-subtle {
  background: var(--cal-neutral-subtle);
}

.cal-badge.is-neutral.is-outline {
  border-color: var(--cal-neutral-border);
}

.cal-badge.is-accent {
  color: var(--cal-accent);
}

.cal-badge.is-accent.is-subtle {
  background: var(--cal-accent-subtle);
}

.cal-badge.is-accent.is-outline {
  border-color: var(--cal-accent-border);
}

.cal-badge.is-success {
  color: var(--cal-success);
}

.cal-badge.is-success.is-subtle {
  background: var(--cal-success-subtle);
}

.cal-badge.is-success.is-outline {
  border-color: var(--cal-success-border);
}

.cal-badge.is-warning {
  color: var(--cal-warning);
}

.cal-badge.is-warning.is-subtle {
  background: var(--cal-warning-subtle);
}

.cal-badge.is-warning.is-outline {
  border-color: var(--cal-warning-border);
}

.cal-badge.is-danger {
  color: var(--cal-danger);
}

.cal-badge.is-danger.is-subtle {
  background: var(--cal-danger-subtle);
}

.cal-badge.is-danger.is-outline {
  border-color: var(--cal-danger-border);
}

.cal-badge.is-info {
  color: var(--cal-info);
}

.cal-badge.is-info.is-subtle {
  background: var(--cal-info-subtle);
}

.cal-badge.is-info.is-outline {
  border-color: var(--cal-info-border);
}
</style>
