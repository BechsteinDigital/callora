<template>
  <span class="cal-skeleton" :style="{ width, height }" aria-hidden="true" />
</template>

<script setup lang="ts">
// A shape holding the place of content that is still loading. Preferred over a
// "Lädt…" line because the layout does not jump once the data lands.
withDefaults(defineProps<{ width?: string; height?: string }>(), { width: '100%', height: '12px' })
</script>

<style scoped lang="scss">
.cal-skeleton {
  display: block;
  border-radius: var(--cal-radius-xs);
  background: linear-gradient(
    90deg,
    var(--cal-surface-hover) 25%,
    var(--cal-surface-active) 37%,
    var(--cal-surface-hover) 63%
  );
  background-size: 400% 100%;
  animation: cal-skeleton-shimmer 1.4s ease infinite;
}

@keyframes cal-skeleton-shimmer {
  0% {
    background-position: 100% 50%;
  }

  100% {
    background-position: 0 50%;
  }
}
</style>
