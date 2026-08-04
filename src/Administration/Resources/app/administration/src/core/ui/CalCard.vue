<template>
  <section class="cal-card" :class="[`is-${padding}`, { 'is-flush': flush }]">
    <header v-if="title || $slots.actions || $slots.header" class="cal-card__head">
      <div class="cal-card__heading">
        <h2 v-if="title" class="cal-card__title">{{ title }}</h2>
        <p v-if="description" class="cal-card__description">{{ description }}</p>
        <slot name="header" />
      </div>
      <div v-if="$slots.actions" class="cal-card__actions"><slot name="actions" /></div>
    </header>
    <div class="cal-card__body"><slot /></div>
    <footer v-if="$slots.footer" class="cal-card__footer"><slot name="footer" /></footer>
  </section>
</template>

<script setup lang="ts">
/**
 * The surface every grouped block sits on. `flush` drops the body padding for
 * content that brings its own edges — a full-bleed table being the main case.
 */
withDefaults(
  defineProps<{
    title?: string
    description?: string
    padding?: 'sm' | 'md' | 'lg'
    flush?: boolean
  }>(),
  { padding: 'md', flush: false },
)
</script>

<style scoped lang="scss">
.cal-card {
  display: flex;
  flex-direction: column;
  background: var(--cal-surface);
  border: 1px solid var(--cal-border);
  border-radius: var(--cal-radius-lg);
  overflow: hidden;
}

.cal-card__head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--cal-space-4);
  padding: var(--cal-space-4) var(--cal-space-5);
  border-bottom: 1px solid var(--cal-border-subtle);
}

.cal-card__heading {
  display: flex;
  flex-direction: column;
  gap: var(--cal-space-1);
  min-width: 0;
}

.cal-card__title {
  font-size: var(--cal-text-lg);
  font-weight: var(--cal-weight-semibold);
}

.cal-card__description {
  font-size: var(--cal-text-md);
  color: var(--cal-text-secondary);
  line-height: var(--cal-leading-normal);
}

.cal-card__actions {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
  flex: none;
}

.cal-card__body {
  flex: 1;
  min-width: 0;
}

.cal-card.is-sm .cal-card__body {
  padding: var(--cal-space-3) var(--cal-space-4);
}

.cal-card.is-md .cal-card__body {
  padding: var(--cal-space-5);
}

.cal-card.is-lg .cal-card__body {
  padding: var(--cal-space-6);
}

.cal-card.is-flush .cal-card__body {
  padding: 0;
}

.cal-card__footer {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--cal-space-2);
  padding: var(--cal-space-3) var(--cal-space-5);
  border-top: 1px solid var(--cal-border-subtle);
  background: var(--cal-bg-subtle);
}
</style>
