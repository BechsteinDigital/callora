<script setup lang="ts">
import { computed } from 'vue'
import type { LayoutBlock, LayoutSection } from './layout-document'

/**
 * Eine Sektion des Canvas mit ihren Blöcken.
 *
 * Der Canvas baut NICHT nach, was der Kompositions-Renderer erzeugt. Jener emittiert Inseln —
 * Platzhalter, die der Browser später mit Vue-Komponenten füllt. Hier liegen die Komponenten
 * schon vor, also rendert der Canvas sie direkt: dieselben Komponenten, ein Schritt weniger.
 *
 * Das Insel-Markup nachzubauen gäbe dem Editor einen zweiten Renderpfad, und ein zweiter Pfad
 * ist einer, der driftet. So kann sich zwischen Canvas und Live nur die *Daten* unterscheiden —
 * und dieser Unterschied ist gewollt (§7.6: simulierte Kontextwerte).
 *
 * **Der Editier-Marker ist ein durchgereichtes Attribut, kein Hüllelement.** Ein Wrapper um
 * jeden Block wäre der bequemere Weg und der falsche: Ein Theme setzt Abstände typischerweise
 * über `.cal-region > * + *`, und dazwischen ein Element zu schieben bricht genau das —
 * `display: contents` erst recht, denn ein Element ohne Box hat auch keine Margins. Vue reicht
 * Nicht-Prop-Attribute an die Wurzel der Komponente durch, also markieren wir den Block selbst.
 */
const props = defineProps<{
  section: LayoutSection
  /** Der Index dieser Sektion im Dokument — Teil der Adresse eines Blocks. */
  sectionIndex: number
  /** Registrierte Blöcke nach id. Ein Block, dessen Plugin nicht geladen ist, fehlt hier. */
  components: Record<string, unknown>
  /** Der ausgewählte Block, sofern er in dieser Sektion liegt. */
  selectedBlockIndex: number | null
}>()

const emit = defineEmits<{ select: [blockIndex: number] }>()

interface PlacedBlock {
  block: LayoutBlock
  /** Der Index im `blocks`-Array der Sektion — die Adresse, nicht die Anzeigereihenfolge. */
  index: number
}

const regions = computed(() => {
  const grouped = new Map<string, PlacedBlock[]>()
  props.section.blocks.forEach((block, index) => {
    const list = grouped.get(block.region) ?? []
    list.push({ block, index })
    grouped.set(block.region, list)
  })

  return [...grouped.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([region, placed]) => ({
      region,
      blocks: [...placed].sort((a, b) => a.block.position - b.block.position),
    }))
})

/**
 * Die Props, die ein Block im Canvas bekommt. Nur statische Bindungen ergeben hier einen Wert —
 * eine Kontext-Bindung hat noch keinen, und einen zu erfinden zeigte dem Redakteur etwas, das
 * die Fläche so nicht rendert.
 */
function propsFor(block: LayoutBlock): Record<string, unknown> {
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
      <template v-for="placed in group.blocks" :key="placed.index">
        <component
          :is="components[placed.block.blockId]"
          v-if="components[placed.block.blockId]"
          v-bind="propsFor(placed.block)"
          :data-cal-editor-block="placed.index"
          :data-cal-editor-selected="placed.index === selectedBlockIndex ? 'true' : undefined"
          @click="emit('select', placed.index)"
        />
        <!--
          Ein verwaister Block — sein Plugin ist nicht geladen. Im Editor benannt stehen lassen,
          nicht weglassen: Das Frontend lässt ihn weg, aber wer hier gestaltet, muss sehen, dass
          da etwas ist, das zurückkommt, sobald das Plugin wieder da ist. Auswählbar bleibt er
          auch, denn seine Einstellungen stehen weiter im Layout.
        -->
        <div
          v-else
          class="cal-canvas__orphan"
          :data-block-id="placed.block.blockId"
          :data-cal-editor-block="placed.index"
          :data-cal-editor-selected="placed.index === selectedBlockIndex ? 'true' : undefined"
          @click="emit('select', placed.index)"
        >
          {{ placed.block.blockId }}
        </div>
      </template>
    </div>
  </div>
</template>
