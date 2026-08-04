<template>
  <label class="cal-switch" :class="{ 'is-disabled': disabled }">
    <input
      v-bind="$attrs"
      type="checkbox"
      role="switch"
      class="cal-switch__input"
      :checked="modelValue"
      :disabled="disabled"
      @change="$emit('update:modelValue', ($event.target as HTMLInputElement).checked)"
    />
    <span class="cal-switch__track"><span class="cal-switch__thumb" /></span>
    <span v-if="$slots.default" class="cal-switch__label"><slot /></span>
  </label>
</template>

<script setup lang="ts">
// For settings that take effect as a state rather than on submit. A checkbox
// under the hood, so keyboard and assistive technology behave natively.
defineOptions({ inheritAttrs: false })

withDefaults(defineProps<{ modelValue: boolean; disabled?: boolean }>(), { disabled: false })

defineEmits<{ 'update:modelValue': [value: boolean] }>()
</script>

<style scoped lang="scss">
.cal-switch {
  display: inline-flex;
  align-items: center;
  gap: var(--cal-space-2);
  cursor: pointer;
  user-select: none;
}

.cal-switch.is-disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.cal-switch__input {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip-path: inset(50%);
}

.cal-switch__track {
  position: relative;
  width: 32px;
  height: 18px;
  padding: 2px;
  border-radius: var(--cal-radius-full);
  background: var(--cal-border-strong);
  transition: background var(--cal-duration-fast) var(--cal-ease);
}

.cal-switch__thumb {
  display: block;
  width: 14px;
  height: 14px;
  border-radius: var(--cal-radius-full);
  background: #fff;
  box-shadow: var(--cal-shadow-sm);
  transition: transform var(--cal-duration-fast) var(--cal-ease);
}

.cal-switch__input:checked + .cal-switch__track {
  background: var(--cal-accent);
}

.cal-switch__input:checked + .cal-switch__track .cal-switch__thumb {
  transform: translateX(14px);
}

.cal-switch__input:focus-visible + .cal-switch__track {
  box-shadow: 0 0 0 3px var(--cal-accent-subtle);
}

.cal-switch__label {
  font-size: var(--cal-text-md);
}
</style>
