<template>
  <div class="cal-select" :class="[`is-${size}`, { 'is-disabled': disabled }]">
    <select
      v-bind="$attrs"
      class="cal-select__field"
      :value="modelValue"
      :disabled="disabled"
      @change="$emit('update:modelValue', ($event.target as HTMLSelectElement).value)"
    >
      <slot />
    </select>
    <CalIcon class="cal-select__chevron" :icon="ChevronDown" size="sm" />
  </div>
</template>

<script setup lang="ts">
import { ChevronDown } from 'lucide-vue-next'
import CalIcon from './CalIcon.vue'

// A native <select> on purpose: it stays keyboard- and screen-reader correct on
// every platform and renders the OS picker on mobile. Only the chrome is ours.
defineOptions({ inheritAttrs: false })

withDefaults(defineProps<{ modelValue: string; size?: 'sm' | 'md'; disabled?: boolean }>(), {
  size: 'md',
  disabled: false,
})

defineEmits<{ 'update:modelValue': [value: string] }>()
</script>

<style scoped lang="scss">
.cal-select {
  position: relative;
  display: flex;
  align-items: center;
  width: 100%;
  background: var(--cal-surface-inset);
  border: 1px solid var(--cal-border);
  border-radius: var(--cal-radius-sm);
  transition: border-color var(--cal-duration-fast) var(--cal-ease);
}

.cal-select.is-sm {
  height: 28px;
}

.cal-select.is-md {
  height: 32px;
}

.cal-select:hover:not(.is-disabled) {
  border-color: var(--cal-border-strong);
}

.cal-select:focus-within {
  border-color: var(--cal-accent);
  box-shadow: 0 0 0 3px var(--cal-accent-subtle);
}

.cal-select.is-disabled {
  opacity: 0.55;
}

.cal-select__field {
  flex: 1;
  min-width: 0;
  height: 100%;
  padding: 0 var(--cal-space-8) 0 var(--cal-space-3);
  border: 0;
  background: transparent;
  color: var(--cal-text);
  font-size: var(--cal-text-md);
  outline: none;
  appearance: none;
  cursor: pointer;
}

.cal-select__field:disabled {
  cursor: not-allowed;
}

.cal-select__field option {
  background: var(--cal-surface-raised);
  color: var(--cal-text);
}

.cal-select__chevron {
  position: absolute;
  right: var(--cal-space-2);
  color: var(--cal-text-muted);
  pointer-events: none;
}
</style>
