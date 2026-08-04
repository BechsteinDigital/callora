<template>
  <div class="cal-page" :class="{ 'is-wide': wide, 'is-narrow': narrow }">
    <slot />
  </div>
</template>

<script setup lang="ts">
// The content frame of a routed view: one place deciding page padding and the
// maximum measure, instead of every module repeating its own padding rule.
withDefaults(defineProps<{ wide?: boolean; narrow?: boolean }>(), { wide: false, narrow: false })
</script>

<style scoped lang="scss">
.cal-page {
  width: 100%;
  max-width: var(--cal-content-max);
  margin: 0 auto;
  padding: var(--cal-space-8) var(--cal-space-8) var(--cal-space-16);
}

/* Wide: tables that would otherwise wrap; narrow: forms and settings, where a
   short measure keeps labels and controls readable. */
.cal-page.is-wide {
  max-width: none;
}

.cal-page.is-narrow {
  max-width: 760px;
}

@media (width <= 900px) {
  .cal-page {
    padding: var(--cal-space-5) var(--cal-space-4) var(--cal-space-12);
  }
}
</style>
