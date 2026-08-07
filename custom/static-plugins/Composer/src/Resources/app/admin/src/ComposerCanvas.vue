<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'
import CanvasSection from './CanvasSection.vue'
import type { BlockAddress, LayoutDocument } from './layout-document'
import type { SectionLayout } from './preview-assets'
import {
  applyScopedSurfaceStyles,
  CANVAS_SCOPE,
  scopeThemeTokens,
} from './scoped-surface-styles'

/**
 * Der Canvas: die Fläche, wie sie aussehen wird, im Admin.
 *
 * Kein iframe und kein zweiter Renderer. Die Blöcke sind dieselben Vue-Komponenten, die die
 * Fläche mountet, aus derselben Registry; das Styling ist dasselbe Stylesheet, gescoped, damit
 * es nicht in die Shell entkommt. Nichts hier nähert das Ergebnis an — anders bleibt eine
 * Vorschau nicht ehrlich, während das Produkt wächst.
 */
const props = defineProps<{
  /** Das bearbeitete Layout-Dokument. */
  document: LayoutDocument
  /** Das Stylesheet der Fläche als Text. Wird vor dem Anwenden gescoped. */
  surfaceCss?: string
  /** Die Token des Themes, wie der Server sie rendern würde. */
  tokens?: Readonly<Record<string, string>>
  /** Die Sektionslayouts des Themes — sie sagen, welche Regionen eine Sektion hat. */
  layouts?: readonly SectionLayout[]
  /** Der ausgewählte Block. */
  selected?: BlockAddress | null
  /**
   * Ob der Editier-Modus aktiv ist. Aktiv fängt der Canvas Klicks auf Blockebene ab; aus
   * verhält sich ein Block wie auf der Fläche — der „Interaktiv testen"-Umschalter aus §7.6.
   */
  editing?: boolean
}>()

const emit = defineEmits<{ select: [address: BlockAddress] }>()

const sections = computed(() => {
  const raw = Array.isArray(props.document?.sections) ? props.document.sections : []
  // Die Anzeigereihenfolge folgt `position`; die Adresse bleibt der Index im Dokument. Beides
  // zu vermischen hieße, dass eine Umsortierung stumm die Auswahl auf einen anderen Block
  // verschiebt.
  return raw
    .map((section, index) => ({ section, index }))
    .sort((a, b) => (a.section.position ?? 0) - (b.section.position ?? 0))
})

/**
 * Die Blöcke, die die Runtime kennt. Bei jedem Render gelesen statt einmal festgehalten: Ein
 * Plugin-Bundle kann nach dem Mounten des Canvas laden, und ein Block, der später auftaucht,
 * soll auftauchen.
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

function selectedIndexIn(sectionIndex: number): number | null {
  return props.selected?.sectionIndex === sectionIndex ? props.selected.blockIndex : null
}

function applyStyles(): void {
  const css = [props.surfaceCss ?? '', props.tokens ? scopeThemeTokens(props.tokens) : ''].join('\n')
  applyScopedSurfaceStyles(css)
}

onMounted(applyStyles)
watch(() => [props.surfaceCss, props.tokens], applyStyles, { deep: true })
</script>

<template>
  <div :class="CANVAS_SCOPE" :data-cal-editing="editing === false ? 'false' : 'true'">
    <CanvasSection
      v-for="entry in sections"
      :key="entry.index"
      :section="entry.section"
      :section-index="entry.index"
      :components="components"
      :layouts="layouts ?? []"
      :selected-block-index="selectedIndexIn(entry.index)"
      @select="emit('select', { sectionIndex: entry.index, blockIndex: $event })"
    />
    <p v-if="sections.length === 0" class="cal-canvas__empty">
      Noch keine Sektion. Ziehen Sie einen Block hierher, um zu beginnen.
    </p>
  </div>
</template>
