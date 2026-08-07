<script setup lang="ts">
import { computed } from 'vue'

/**
 * One section of the canvas, with its blocks.
 *
 * The canvas does NOT rebuild what the composition renderer produces. That renderer emits islands
 * — placeholders the browser later fills with Vue components. Here the components are already at
 * hand, so the canvas renders them directly: same components, one step shorter.
 *
 * Rebuilding the island markup instead would give the editor a second rendering path, and a second
 * path is a path that drifts. This way there is only one thing that can differ between canvas and
 * live — the data — and that difference is deliberate (§7.6: simulated context values).
 */
interface CanvasBlock {
  blockId: string
  region: string
  position: number
  config?: Record<string, { source: string; value?: unknown; key?: string; path?: string }>
}

interface CanvasSectionModel {
  layout: string
  position: number
  spacing?: string
  surfaceRole?: string
  blocks: CanvasBlock[]
}

const props = defineProps<{
  section: CanvasSectionModel
  /** Registered blocks by id. A block whose plugin is not loaded is missing here. */
  components: Record<string, unknown>
}>()

const regions = computed(() => {
  const grouped = new Map<string, CanvasBlock[]>()
  for (const block of props.section.blocks) {
    const list = grouped.get(block.region) ?? []
    list.push(block)
    grouped.set(block.region, list)
  }

  return [...grouped.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([region, blocks]) => ({
      region,
      blocks: [...blocks].sort((a, b) => a.position - b.position),
    }))
})

/**
 * The props a block gets in the canvas. Only static bindings resolve to a value here — a context
 * binding has no value yet, and inventing one would show the editor something the surface will
 * not render.
 */
function propsFor(block: CanvasBlock): Record<string, unknown> {
  const resolved: Record<string, unknown> = {}
  for (const [name, binding] of Object.entries(block.config ?? {})) {
    if (binding.source === 'static') {
      resolved[name] = binding.value
    }
  }

  return resolved
}
</script>

<template>
  <div
    class="cal-section"
    :data-cal-layout="section.layout"
    :data-cal-spacing="section.spacing"
    :data-cal-surface="section.surfaceRole"
  >
    <div v-for="group in regions" :key="group.region" class="cal-region" :data-cal-region="group.region">
      <template v-for="block in group.blocks" :key="`${block.region}:${block.position}`">
        <component
          :is="components[block.blockId]"
          v-if="components[block.blockId]"
          v-bind="propsFor(block)"
        />
        <!--
          Ein verwaister Block — sein Plugin ist nicht geladen. Im Editor benannt stehen lassen,
          nicht weglassen: Das Frontend lässt ihn weg, aber wer hier gestaltet, muss sehen, dass
          da etwas ist, das zurückkommt, sobald das Plugin wieder da ist.
        -->
        <div v-else class="cal-canvas__orphan" :data-block-id="block.blockId">
          {{ block.blockId }}
        </div>
      </template>
    </div>
  </div>
</template>
