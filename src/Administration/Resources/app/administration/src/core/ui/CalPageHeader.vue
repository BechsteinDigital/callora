<template>
  <header class="cal-page-header">
    <RouterLink v-if="backTo" class="cal-page-header__back" :to="backTo">
      <CalIcon :icon="ArrowLeft" size="sm" />
      {{ backLabel }}
    </RouterLink>
    <div class="cal-page-header__row">
      <div class="cal-page-header__heading">
        <div class="cal-page-header__title-row">
          <h1 class="cal-page-header__title">{{ title }}</h1>
          <slot name="title-suffix" />
        </div>
        <p v-if="description" class="cal-page-header__description">{{ description }}</p>
      </div>
      <div v-if="$slots.actions" class="cal-page-header__actions"><slot name="actions" /></div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { ArrowLeft } from 'lucide-vue-next'
import { RouterLink } from 'vue-router'
import CalIcon from './CalIcon.vue'

/**
 * The masthead of every page: title, one line of orientation, and the actions
 * that belong to the page as a whole. Detail views add `backTo` to get the
 * return path that each module previously improvised.
 */
withDefaults(
  defineProps<{
    title: string
    description?: string
    backTo?: string
    backLabel?: string
  }>(),
  { backLabel: 'Zurück' },
)
</script>

<style scoped lang="scss">
.cal-page-header {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-2);
  margin-bottom: var(--cal-space-6);
}

.cal-page-header__back {
  display: inline-flex;
  align-items: center;
  gap: var(--cal-space-1);
  align-self: flex-start;
  font-size: var(--cal-text-md);
  color: var(--cal-text-muted);
}

.cal-page-header__back:hover {
  color: var(--cal-text);
  text-decoration: none;
}

.cal-page-header__row {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--cal-space-4);
  flex-wrap: wrap;
}

.cal-page-header__heading {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-1);
  min-width: 0;
}

.cal-page-header__title-row {
  display: flex;
  align-items: center;
  gap: var(--cal-space-3);
}

.cal-page-header__title {
  font-size: var(--cal-text-2xl);
  font-weight: var(--cal-weight-semibold);
  letter-spacing: -0.01em;
}

.cal-page-header__description {
  font-size: var(--cal-text-base);
  color: var(--cal-text-secondary);
  line-height: var(--cal-leading-normal);
  max-width: 68ch;
}

.cal-page-header__actions {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
  flex: none;
}
</style>
