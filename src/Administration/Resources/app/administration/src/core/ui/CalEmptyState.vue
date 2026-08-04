<template>
  <div class="cal-empty" :class="{ 'is-compact': compact }">
    <div v-if="icon" class="cal-empty__icon">
      <CalIcon :icon="icon" size="lg" />
    </div>
    <p class="cal-empty__title">{{ title }}</p>
    <p v-if="description" class="cal-empty__description">{{ description }}</p>
    <div v-if="$slots.action" class="cal-empty__action"><slot name="action" /></div>
  </div>
</template>

<script setup lang="ts">
import type { Component } from 'vue'
import CalIcon from './CalIcon.vue'

/**
 * What a view shows instead of nothing. It always names the next step — an empty
 * screen without an explanation and an action is a dead end for the operator.
 */
withDefaults(defineProps<{ title: string; description?: string; icon?: Component; compact?: boolean }>(), {
  compact: false,
})
</script>

<style scoped lang="scss">
.cal-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: var(--cal-space-2);
  padding: var(--cal-space-12) var(--cal-space-6);
  text-align: center;
}

.cal-empty.is-compact {
  padding: var(--cal-space-8) var(--cal-space-4);
}

.cal-empty__icon {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  margin-bottom: var(--cal-space-1);
  border-radius: var(--cal-radius-full);
  background: var(--cal-neutral-subtle);
  color: var(--cal-text-muted);
}

.cal-empty__title {
  font-size: var(--cal-text-base);
  font-weight: var(--cal-weight-medium);
  color: var(--cal-text);
}

.cal-empty__description {
  font-size: var(--cal-text-md);
  color: var(--cal-text-muted);
  max-width: 46ch;
  line-height: var(--cal-leading-normal);
}

.cal-empty__action {
  margin-top: var(--cal-space-2);
}
</style>
