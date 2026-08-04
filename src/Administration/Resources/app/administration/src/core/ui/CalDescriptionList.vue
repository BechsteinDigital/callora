<template>
  <dl class="cal-dl" :class="{ 'is-stacked': stacked }">
    <template v-for="item in items" :key="item.term">
      <dt class="cal-dl__term">{{ item.term }}</dt>
      <dd class="cal-dl__value" :class="{ 'is-mono': item.mono }">
        <slot :name="item.term" :item="item">{{ item.value || '—' }}</slot>
      </dd>
    </template>
  </dl>
</template>

<script setup lang="ts">
import type { DescriptionItem } from './descriptionList'

// Read-only key/value facts — identity, scopes, metadata. A slot per term lets a
// view render a badge or link where plain text is not enough.
withDefaults(defineProps<{ items: readonly DescriptionItem[]; stacked?: boolean }>(), { stacked: false })
</script>

<style scoped lang="scss">
.cal-dl {
  display: grid;
  grid-template-columns: minmax(120px, max-content) 1fr;
  gap: var(--cal-space-3) var(--cal-space-6);
  margin: 0;
  font-size: var(--cal-text-md);
}

.cal-dl.is-stacked {
  grid-template-columns: 1fr;
  gap: var(--cal-space-1);
}

.cal-dl__term {
  color: var(--cal-text-muted);
}

.cal-dl__value {
  margin: 0;
  color: var(--cal-text);
  min-width: 0;
  overflow-wrap: anywhere;
}

.cal-dl__value.is-mono {
  font-family: var(--cal-font-mono);
  font-size: var(--cal-text-sm);
}

@media (width <= 600px) {
  .cal-dl {
    grid-template-columns: 1fr;
    gap: var(--cal-space-1);
  }

  .cal-dl__term {
    margin-top: var(--cal-space-2);
  }
}
</style>
