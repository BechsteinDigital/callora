<template>
  <input
    class="cal-input"
    :value="modelValue"
    :type="type"
    @input="$emit('update:modelValue', ($event.target as HTMLInputElement).value)"
  />
</template>

<script setup lang="ts">
defineProps<{ modelValue: string; type?: string }>()
defineEmits<{ 'update:modelValue': [value: string] }>()
</script>

<style scoped lang="scss">
.cal-input {
  width: 100%;
  padding: calc(var(--cal-space) * 1.25);
  border: 1px solid var(--cal-color-muted);
  border-radius: var(--cal-radius);
  background: var(--cal-color-surface);
  color: var(--cal-color-text);
  font: inherit;
}
</style>
