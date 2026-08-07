<script setup lang="ts">
import { computed } from 'vue'
import type { DropTarget, LayoutBlock, LayoutSection } from './layout-document'
import type { SectionLayout } from './preview-assets'
import { regionsOf } from './section-layouts'
import { DRAG_FORMAT } from './block-palette'

/**
 * Eine Sektion des Canvas mit ihren Blöcken.
 *
 * Der Canvas baut NICHT nach, was der Kompositions-Renderer erzeugt. Jener emittiert Inseln —
 * Platzhalter, die der Browser später mit Vue-Komponenten füllt. Hier liegen die Komponenten
 * schon vor, also rendert der Canvas sie direkt: dieselben Komponenten, ein Schritt weniger.
 *
 * **Der Editier-Marker ist ein durchgereichtes Attribut, kein Hüllelement.** Ein Wrapper um
 * jeden Block wäre der bequemere Weg und der falsche: Ein Theme setzt Abstände typischerweise
 * über `.cal-region > * + *`, und dazwischen ein Element zu schieben bricht genau das —
 * `display: contents` erst recht, denn ein Element ohne Box hat auch keine Margins. Vue reicht
 * Nicht-Prop-Attribute an die Wurzel der Komponente durch, also markieren wir den Block selbst.
 *
 * **Die Ablegezonen gibt es nur, WÄHREND gezogen wird.** Aus demselben Grund: Sie sind echte
 * Elemente zwischen den Blöcken, und dauerhaft eingefügt bräche jede `+`- und `>`-Regel des
 * Themes. Im Ruhezustand ist der Baum hier deshalb exakt der der Fläche; dass er sich beim
 * Ziehen kurz ändert, sieht man ohnehin — es ist der ganze Zweck.
 */
const props = defineProps<{
  section: LayoutSection
  /** Der Index dieser Sektion im Dokument — Teil der Adresse eines Blocks. */
  sectionIndex: number
  /** Registrierte Blöcke nach id. Ein Block, dessen Plugin nicht geladen ist, fehlt hier. */
  components: Record<string, unknown>
  /** Die Sektionslayouts des Themes — sie sagen, welche Regionen es gibt. */
  layouts: readonly SectionLayout[]
  /** Der ausgewählte Block, sofern er in dieser Sektion liegt. */
  selectedBlockIndex: number | null
  /** Ob gerade etwas gezogen wird. Nur dann gibt es Ablegezonen. */
  dragging?: boolean
}>()

const emit = defineEmits<{
  select: [blockIndex: number]
  dropBlock: [target: DropTarget, data: string]
  dragBlock: [blockIndex: number]
}>()

interface PlacedBlock {
  block: LayoutBlock
  /** Der Index im `blocks`-Array der Sektion — die Adresse, nicht die Anzeigereihenfolge. */
  index: number
}

/**
 * Alle Regionen des Layouts, auch die leeren.
 *
 * Eine leere Region muss sichtbar sein, sonst gibt es keinen Ort, an den man etwas ziehen
 * könnte — und eine zweispaltige Sektion mit einer leeren Spalte sähe aus wie eine einspaltige.
 */
const regions = computed(() => {
  const placed = new Map<string, PlacedBlock[]>()
  props.section.blocks.forEach((block, index) => {
    const list = placed.get(block.region) ?? []
    list.push({ block, index })
    placed.set(block.region, list)
  })

  return regionsOf(props.section, props.layouts).map((region) => ({
    ...region,
    blocks: [...(placed.get(region.regionKey) ?? [])].sort(
      (a, b) => a.block.position - b.block.position,
    ),
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

function startDrag(event: DragEvent, blockIndex: number): void {
  event.dataTransfer?.setData(
    DRAG_FORMAT,
    JSON.stringify({ kind: 'move', sectionIndex: props.sectionIndex, blockIndex }),
  )
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'move'
  }
  emit('dragBlock', blockIndex)
}

/**
 * `preventDefault` ist es, was eine Zone überhaupt zum Ziel macht — ohne den Aufruf lehnt der
 * Browser den Drop ab, und zwar wortlos.
 */
function allowDrop(event: DragEvent): void {
  event.preventDefault()
}

function handleDrop(event: DragEvent, region: string, index: number): void {
  event.preventDefault()
  const data = event.dataTransfer?.getData(DRAG_FORMAT) ?? ''
  emit('dropBlock', { sectionIndex: props.sectionIndex, region, index }, data)
}
</script>

<template>
  <div
    class="cal-section"
    :data-cal-layout="section.layout"
    :data-cal-spacing="section.spacing"
    :data-cal-surface="section.surfaceRole"
  >
    <div
      v-for="region in regions"
      :key="region.regionKey"
      class="cal-region"
      :data-cal-region="region.regionKey"
      :data-cal-editor-undeclared="region.declared ? undefined : 'true'"
    >
      <template v-for="(placed, position) in region.blocks" :key="placed.index">
        <div
          v-if="dragging"
          class="cal-canvas__dropzone"
          :data-cal-drop-region="region.regionKey"
          :data-cal-drop-index="position"
          @dragover="allowDrop"
          @drop="handleDrop($event, region.regionKey, position)"
        />
        <component
          :is="components[placed.block.blockId]"
          v-if="components[placed.block.blockId]"
          v-bind="propsFor(placed.block)"
          :data-cal-editor-block="placed.index"
          :data-cal-editor-selected="placed.index === selectedBlockIndex ? 'true' : undefined"
          draggable="true"
          @click="emit('select', placed.index)"
          @dragstart="startDrag($event, placed.index)"
        />
        <!--
          Ein verwaister Block — sein Plugin ist nicht geladen. Im Editor benannt stehen lassen,
          nicht weglassen: Das Frontend lässt ihn weg, aber wer hier gestaltet, muss sehen, dass
          da etwas ist, das zurückkommt, sobald das Plugin wieder da ist. Ziehen lässt er sich
          auch — sonst wäre eine Seite, in der ein Plugin fehlt, nicht mehr umzusortieren.
        -->
        <div
          v-else
          class="cal-canvas__orphan"
          :data-block-id="placed.block.blockId"
          :data-cal-editor-block="placed.index"
          :data-cal-editor-selected="placed.index === selectedBlockIndex ? 'true' : undefined"
          draggable="true"
          @click="emit('select', placed.index)"
          @dragstart="startDrag($event, placed.index)"
        >
          {{ placed.block.blockId }}
        </div>
      </template>

      <!--
        Die Zone HINTER dem letzten Block. Ohne sie ließe sich nur vor etwas ablegen, und ans
        Ende einer Region käme man nur, indem man etwas anderes verschiebt.
      -->
      <div
        v-if="dragging"
        class="cal-canvas__dropzone"
        :class="{ 'cal-canvas__dropzone--empty': region.blocks.length === 0 }"
        :data-cal-drop-region="region.regionKey"
        :data-cal-drop-index="region.blocks.length"
        @dragover="allowDrop"
        @drop="handleDrop($event, region.regionKey, region.blocks.length)"
      />

      <p v-if="region.blocks.length === 0 && !dragging" class="cal-canvas__region-empty">
        {{ region.label }}
      </p>
      <p v-else-if="!region.declared" class="cal-canvas__region-orphan">
        Region „{{ region.regionKey }}" gibt es in diesem Layout nicht.
      </p>
    </div>
  </div>
</template>
