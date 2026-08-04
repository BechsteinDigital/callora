<template>
  <component
    :is="component"
    :to="to"
    :type="to ? undefined : type"
    :disabled="isDisabled"
    :aria-disabled="isDisabled || undefined"
    :aria-busy="loading || undefined"
    class="cal-btn"
    :class="[`is-${variant}`, `is-${size}`, { 'is-block': block, 'is-icon-only': iconOnly, 'is-loading': loading }]"
  >
    <CalSpinner v-if="loading" class="cal-btn__spinner" :size="size === 'lg' ? 'md' : 'sm'" />
    <CalIcon v-else-if="icon" :icon="icon" :size="size === 'lg' ? 'lg' : 'sm'" />
    <span v-if="!iconOnly" class="cal-btn__label"><slot /></span>
    <CalIcon v-if="trailingIcon && !iconOnly" :icon="trailingIcon" :size="size === 'lg' ? 'lg' : 'sm'" />
  </component>
</template>

<script setup lang="ts">
import { computed, type Component } from 'vue'
import { RouterLink } from 'vue-router'
import CalIcon from './CalIcon.vue'
import CalSpinner from './CalSpinner.vue'

/**
 * The single button in the shell. A `to` turns it into a RouterLink that still
 * looks like a button — the pattern every list view needs for "create new",
 * which previously was a hand-styled <RouterLink> in each module.
 */
const props = withDefaults(
  defineProps<{
    variant?: 'primary' | 'secondary' | 'ghost' | 'danger' | 'danger-ghost'
    size?: 'sm' | 'md' | 'lg'
    type?: 'button' | 'submit' | 'reset'
    to?: string
    icon?: Component
    trailingIcon?: Component
    iconOnly?: boolean
    loading?: boolean
    disabled?: boolean
    block?: boolean
  }>(),
  { variant: 'secondary', size: 'md', type: 'button', iconOnly: false, loading: false, disabled: false, block: false },
)

const component = computed(() => (props.to ? RouterLink : 'button'))
// A busy button must not fire again; links cannot carry `disabled`, so they rely
// on aria-disabled plus the pointer-events lock in CSS.
const isDisabled = computed(() => (props.to ? undefined : props.disabled || props.loading))
</script>

<style scoped lang="scss">
.cal-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--cal-space-2);
  border: 1px solid transparent;
  border-radius: var(--cal-radius-sm);
  font-weight: var(--cal-weight-medium);
  white-space: nowrap;
  text-decoration: none;
  cursor: pointer;
  transition:
    background var(--cal-duration-fast) var(--cal-ease),
    border-color var(--cal-duration-fast) var(--cal-ease),
    color var(--cal-duration-fast) var(--cal-ease);
}

.cal-btn:hover {
  text-decoration: none;
}

/* ------------------------------------------------------------------ Größen */
.cal-btn.is-sm {
  height: 26px;
  padding: 0 var(--cal-space-2);
  font-size: var(--cal-text-sm);
}

.cal-btn.is-md {
  height: 32px;
  padding: 0 var(--cal-space-3);
  font-size: var(--cal-text-md);
}

.cal-btn.is-lg {
  height: 38px;
  padding: 0 var(--cal-space-4);
  font-size: var(--cal-text-base);
}

.cal-btn.is-icon-only.is-sm {
  width: 26px;
  padding: 0;
}

.cal-btn.is-icon-only.is-md {
  width: 32px;
  padding: 0;
}

.cal-btn.is-icon-only.is-lg {
  width: 38px;
  padding: 0;
}

.cal-btn.is-block {
  display: flex;
  width: 100%;
}

/* --------------------------------------------------------------- Varianten */
.cal-btn.is-primary {
  background: var(--cal-accent);
  color: var(--cal-accent-contrast);
}

.cal-btn.is-primary:hover:not(:disabled) {
  background: var(--cal-accent-hover);
}

.cal-btn.is-primary:active:not(:disabled) {
  background: var(--cal-accent-active);
}

.cal-btn.is-secondary {
  background: var(--cal-surface-raised);
  border-color: var(--cal-border);
  color: var(--cal-text);
}

.cal-btn.is-secondary:hover:not(:disabled) {
  background: var(--cal-surface-hover);
  border-color: var(--cal-border-strong);
}

.cal-btn.is-ghost {
  background: transparent;
  color: var(--cal-text-secondary);
}

.cal-btn.is-ghost:hover:not(:disabled) {
  background: var(--cal-surface-hover);
  color: var(--cal-text);
}

.cal-btn.is-danger {
  background: var(--cal-danger);
  color: #fff;
}

.cal-btn.is-danger:hover:not(:disabled) {
  background: var(--cal-danger-hover);
}

.cal-btn.is-danger-ghost {
  background: transparent;
  color: var(--cal-danger);
}

.cal-btn.is-danger-ghost:hover:not(:disabled) {
  background: var(--cal-danger-subtle);
}

/* ------------------------------------------------------------- Zustände */
.cal-btn:disabled,
.cal-btn[aria-disabled='true'] {
  opacity: 0.45;
  cursor: not-allowed;
  pointer-events: none;
}

.cal-btn__label {
  overflow: hidden;
  text-overflow: ellipsis;
}
</style>
