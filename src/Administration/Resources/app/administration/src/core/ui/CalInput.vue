<template>
  <div class="cal-input" :class="[`is-${size}`, { 'is-invalid': invalid, 'is-disabled': disabled, 'has-icon': icon }]">
    <CalIcon v-if="icon" class="cal-input__icon" :icon="icon" size="sm" />
    <input
      v-bind="$attrs"
      class="cal-input__field"
      :value="modelValue"
      :type="type"
      :disabled="disabled"
      :placeholder="placeholder"
      :aria-invalid="invalid || undefined"
      @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
    />
    <span v-if="$slots.suffix" class="cal-input__suffix"><slot name="suffix" /></span>
  </div>
</template>

<script setup lang="ts">
import type { Component } from 'vue'
import CalIcon from './CalIcon.vue'

// inheritAttrs is off so `name`, `autocomplete`, `maxlength` and friends land on
// the <input> itself rather than the wrapper — tests and browsers both address
// fields by their name attribute.
defineOptions({ inheritAttrs: false })

withDefaults(
  defineProps<{
    modelValue: string
    type?: string
    placeholder?: string
    icon?: Component
    size?: 'sm' | 'md'
    invalid?: boolean
    disabled?: boolean
  }>(),
  { type: 'text', size: 'md', invalid: false, disabled: false },
)

defineEmits<{ 'update:modelValue': [value: string] }>()
</script>

<style scoped lang="scss">
.cal-input {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
  width: 100%;
  padding: 0 var(--cal-space-3);
  background: var(--cal-surface-inset);
  border: 1px solid var(--cal-border);
  border-radius: var(--cal-radius-sm);
  color: var(--cal-text-muted);
  transition:
    border-color var(--cal-duration-fast) var(--cal-ease),
    background var(--cal-duration-fast) var(--cal-ease);
}

.cal-input.is-sm {
  height: 28px;
}

.cal-input.is-md {
  height: 32px;
}

.cal-input:hover:not(.is-disabled) {
  border-color: var(--cal-border-strong);
}

.cal-input:focus-within {
  border-color: var(--cal-accent);
  background: var(--cal-surface);
  box-shadow: 0 0 0 3px var(--cal-accent-subtle);
}

.cal-input.is-invalid {
  border-color: var(--cal-danger);
}

.cal-input.is-invalid:focus-within {
  box-shadow: 0 0 0 3px var(--cal-danger-subtle);
}

.cal-input.is-disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.cal-input__field {
  flex: 1;
  min-width: 0;
  height: 100%;
  padding: 0;
  border: 0;
  background: transparent;
  color: var(--cal-text);
  font-size: var(--cal-text-md);
  outline: none;
}

.cal-input__field::placeholder {
  color: var(--cal-text-muted);
}

.cal-input__field:disabled {
  cursor: not-allowed;
}

.cal-input__suffix {
  display: flex;
  align-items: center;
  color: var(--cal-text-muted);
  font-size: var(--cal-text-sm);
}
</style>
