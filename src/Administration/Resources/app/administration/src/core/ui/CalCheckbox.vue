<template>
  <label class="cal-checkbox" :class="{ 'is-disabled': disabled }">
    <input
      v-bind="$attrs"
      type="checkbox"
      class="cal-checkbox__input"
      :checked="modelValue"
      :disabled="disabled"
      @change="$emit('update:modelValue', ($event.target as HTMLInputElement).checked)"
    />
    <span class="cal-checkbox__box">
      <CalIcon v-if="modelValue" :icon="Check" size="sm" />
    </span>
    <span v-if="$slots.default" class="cal-checkbox__label"><slot /></span>
  </label>
</template>

<script setup lang="ts">
import { Check } from 'lucide-vue-next'
import CalIcon from './CalIcon.vue'

// The real <input type=checkbox> stays in the DOM (visually hidden) so form
// semantics, labels and `find('input[name=…]')` in tests keep working.
defineOptions({ inheritAttrs: false })

withDefaults(defineProps<{ modelValue: boolean; disabled?: boolean }>(), { disabled: false })

defineEmits<{ 'update:modelValue': [value: boolean] }>()
</script>

<style scoped lang="scss">
.cal-checkbox {
  display: inline-flex;
  align-items: center;
  gap: var(--cal-space-2);
  cursor: pointer;
  user-select: none;
}

.cal-checkbox.is-disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.cal-checkbox__input {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}

.cal-checkbox__box {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  border: 1px solid var(--cal-border-strong);
  border-radius: var(--cal-radius-xs);
  background: var(--cal-surface-inset);
  color: var(--cal-accent-contrast);
  transition:
    background var(--cal-duration-fast) var(--cal-ease),
    border-color var(--cal-duration-fast) var(--cal-ease);
}

.cal-checkbox__input:checked + .cal-checkbox__box {
  background: var(--cal-accent);
  border-color: var(--cal-accent);
}

.cal-checkbox__input:focus-visible + .cal-checkbox__box {
  border-color: var(--cal-accent);
  box-shadow: 0 0 0 3px var(--cal-accent-subtle);
}

.cal-checkbox__label {
  font-size: var(--cal-text-md);
  color: var(--cal-text);
}
</style>
