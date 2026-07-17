<template>
  <button class="cal-btn" :type="type"><slot /></button>
</template>

<script setup lang="ts">
defineProps<{ type?: 'button' | 'submit' }>()
</script>

<style scoped lang="scss">
.cal-btn {
  padding: calc(var(--cal-space) * 1.25) calc(var(--cal-space) * 2);
  border: 0;
  border-radius: var(--cal-radius);
  background: var(--cal-color-accent);
  color: #fff;
  font: inherit;
  cursor: pointer;
}

.cal-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
