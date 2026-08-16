<script setup lang="ts">
import { computed } from 'vue'
import type { SurfaceContext } from './surface-context'
import { isSurfaceViewVisible, type SurfaceRegistry } from './surface-registry'
import { bundlesSettled } from './bundle-readiness'

const props = defineProps<{ context: SurfaceContext; registry: SurfaceRegistry }>()

// The host renders whatever plugins registered — nothing else. An empty surface is a
// valid state (no plugin contributed a view yet), shown as a neutral placeholder.
//
// Erst nachdem der Ladeversuch vorbei ist: Vorher stand der Platzhalter schon da, während die
// Bundles noch unterwegs waren — „Keine Oberfläche registriert." als Aussage über einen Zustand,
// der eine Sekunde später nicht mehr galt (#296).
const views = computed(() =>
  props.registry.views.filter((view) => isSurfaceViewVisible(view, props.context.surfaceKey)),
)
</script>

<template>
  <div class="callora-surface">
    <component
      :is="view.component"
      v-for="view in views"
      :key="view.id"
      :context="context"
    />
    <p
      v-if="views.length === 0 && bundlesSettled"
      class="callora-surface__empty"
      data-testid="surface-empty"
    >
      Keine Oberfläche registriert.
    </p>
  </div>
</template>
