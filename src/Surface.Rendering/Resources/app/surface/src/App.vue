<script setup lang="ts">
import { computed } from 'vue'
import type { SurfaceContext } from './surface-context'
import type { SurfaceRegistry } from './surface-registry'

const props = defineProps<{ context: SurfaceContext; registry: SurfaceRegistry }>()

// The host renders whatever plugins registered — nothing else. An empty surface is a
// valid state (no plugin contributed a view yet), shown as a neutral placeholder.
const views = computed(() => props.registry.views)
</script>

<template>
  <div class="callora-surface">
    <component
      :is="view.component"
      v-for="view in views"
      :key="view.id"
      :context="context"
    />
    <p v-if="views.length === 0" class="callora-surface__empty" data-testid="surface-empty">
      Keine Oberfläche registriert.
    </p>
  </div>
</template>
