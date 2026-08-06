<script setup lang="ts">
import { ref } from 'vue'
import type { SurfaceContext } from '@callora/surface'

// The runtime passes the resolved SurfaceContext as a `context` prop.
defineProps<{ context: SurfaceContext }>()

// A tiny bit of state, to prove the plugin runs inside the runtime's live Vue instance.
const clicks = ref(0)
</script>

<template>
  <section class="surface-demo">
    <h1>Hello from the Surface Demo plugin</h1>
    <p>
      Rendered in workspace <strong>{{ context.workspaceKey }}</strong>,
      surface <strong>{{ context.surfaceKey }}</strong>.
    </p>
    <button type="button" data-testid="counter" @click="clicks++">
      Clicked {{ clicks }} time{{ clicks === 1 ? '' : 's' }}
    </button>
  </section>
</template>

<style scoped>
.surface-demo {
  padding: var(--cal-space-4, 1rem);
  font-family: var(--cal-font-sans, system-ui, sans-serif);
  color: var(--cal-color-fg, #1a1a1a);
}
.surface-demo button {
  margin-top: 0.75rem;
  padding: 0.4rem 0.9rem;
  border: 1px solid var(--cal-color-muted, #6b7280);
  border-radius: 6px;
  background: transparent;
  cursor: pointer;
}
</style>
