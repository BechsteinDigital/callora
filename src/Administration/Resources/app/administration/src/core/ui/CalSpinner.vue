<template>
  <span class="cal-spinner" :class="`is-${size}`" role="status" :aria-label="label" />
</template>

<script setup lang="ts">
withDefaults(defineProps<{ size?: 'sm' | 'md' | 'lg'; label?: string }>(), {
  size: 'md',
  label: 'Lädt',
})
</script>

<style scoped lang="scss">
.cal-spinner {
  display: inline-block;
  flex: none;
  border: 2px solid currentcolor;
  border-right-color: transparent;
  border-radius: var(--cal-radius-full);
  opacity: 0.7;
  animation: cal-spin 620ms linear infinite;
}

.cal-spinner.is-sm {
  width: 12px;
  height: 12px;
}

.cal-spinner.is-md {
  width: 16px;
  height: 16px;
}

.cal-spinner.is-lg {
  width: 24px;
  height: 24px;
  border-width: 2.5px;
}

@keyframes cal-spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
