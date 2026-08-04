<template>
  <TabsRoot v-model="active" class="cal-tabs">
    <TabsList class="cal-tabs__list">
      <TabsTrigger v-for="tab in tabs" :key="tab.value" class="cal-tabs__trigger" :value="tab.value">
        <CalIcon v-if="tab.icon" :icon="tab.icon" size="sm" />
        {{ tab.label }}
        <span v-if="tab.count !== undefined" class="cal-tabs__count">{{ tab.count }}</span>
      </TabsTrigger>
    </TabsList>
    <TabsContent v-for="tab in tabs" :key="tab.value" class="cal-tabs__content" :value="tab.value">
      <slot :name="tab.value" />
    </TabsContent>
  </TabsRoot>
</template>

<script setup lang="ts">
import { TabsContent, TabsList, TabsRoot, TabsTrigger } from 'radix-vue'
import CalIcon from './CalIcon.vue'
import type { TabItem } from './tabs'

// Radix owns roving focus and the aria wiring; we only supply the surface.
defineProps<{ tabs: readonly TabItem[] }>()

const active = defineModel<string>({ required: true })
</script>

<style scoped lang="scss">
.cal-tabs {
  display: flex;
  flex-direction: column;
}

.cal-tabs__list {
  display: flex;
  align-items: center;
  gap: var(--cal-space-1);
  border-bottom: 1px solid var(--cal-border);
  margin-bottom: var(--cal-space-5);
  overflow-x: auto;
}

.cal-tabs__trigger {
  display: inline-flex;
  align-items: center;
  gap: var(--cal-space-2);
  padding: var(--cal-space-2) var(--cal-space-3);
  margin-bottom: -1px;
  border: 0;
  border-bottom: 2px solid transparent;
  background: none;
  color: var(--cal-text-muted);
  font-size: var(--cal-text-md);
  font-weight: var(--cal-weight-medium);
  white-space: nowrap;
  cursor: pointer;
  transition:
    color var(--cal-duration-fast) var(--cal-ease),
    border-color var(--cal-duration-fast) var(--cal-ease);
}

.cal-tabs__trigger:hover {
  color: var(--cal-text);
}

.cal-tabs__trigger[data-state='active'] {
  color: var(--cal-text);
  border-bottom-color: var(--cal-accent);
}

.cal-tabs__count {
  padding: 0 var(--cal-space-1);
  border-radius: var(--cal-radius-full);
  background: var(--cal-neutral-subtle);
  font-size: var(--cal-text-xs);
  font-variant-numeric: tabular-nums;
}

.cal-tabs__content:focus {
  outline: none;
}
</style>
