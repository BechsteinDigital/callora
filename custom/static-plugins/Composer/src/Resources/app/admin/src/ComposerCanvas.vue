<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'
import CanvasSection from './CanvasSection.vue'
import {
  applyScopedSurfaceStyles,
  CANVAS_SCOPE,
  scopeThemeTokens,
} from './scoped-surface-styles'

/**
 * The canvas: the surface as it will look, inside the admin.
 *
 * Not an iframe and not a second renderer. The blocks are the same Vue components the surface
 * mounts, taken from the same registry; the styling is the same stylesheet, scoped so it does not
 * escape into the shell. There is nothing here that approximates the result — which is the only
 * way a preview stays honest as the product grows.
 */
const props = defineProps<{
  /** The layout document being edited. */
  document: { sections?: unknown[] }
  /** The surface stylesheet, as text. Scoped before it is applied. */
  surfaceCss?: string
  /** The theme's tokens, as the server would render them. */
  tokens?: Readonly<Record<string, string>>
}>()

const sections = computed(() => {
  const raw = Array.isArray(props.document?.sections) ? props.document.sections : []
  return [...raw].sort((a: any, b: any) => (a?.position ?? 0) - (b?.position ?? 0)) as any[]
})

/**
 * The blocks the runtime knows about. Read on every render rather than captured once: a plugin
 * bundle may load after the canvas mounted, and a block that appears later should appear.
 */
const components = computed<Record<string, unknown>>(() => {
  const registry = (globalThis as { calloraSurface?: { blocks?: { blocks: { id: string; component: unknown }[] } } })
    .calloraSurface?.blocks
  const known: Record<string, unknown> = {}
  for (const block of registry?.blocks ?? []) {
    known[block.id] = block.component
  }

  return known
})

function applyStyles(): void {
  const css = [props.surfaceCss ?? '', props.tokens ? scopeThemeTokens(props.tokens) : ''].join('\n')
  applyScopedSurfaceStyles(css)
}

onMounted(applyStyles)
watch(() => [props.surfaceCss, props.tokens], applyStyles, { deep: true })
</script>

<template>
  <div :class="CANVAS_SCOPE">
    <CanvasSection
      v-for="(section, index) in sections"
      :key="index"
      :section="section"
      :components="components"
    />
    <p v-if="sections.length === 0" class="cal-canvas__empty">
      Noch keine Sektion. Ziehen Sie einen Block hierher, um zu beginnen.
    </p>
  </div>
</template>
