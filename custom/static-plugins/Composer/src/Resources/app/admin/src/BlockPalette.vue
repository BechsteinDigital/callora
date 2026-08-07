<script setup lang="ts">
import { computed } from 'vue'
import type { BlockCategory, BlockDefinition } from '@callora/surface'
import { DRAG_FORMAT, paletteGroups } from './block-palette'

/**
 * Die Palette: was sich einsetzen lässt.
 *
 * Sie liest dieselbe Registry, aus der der Canvas seine Komponenten nimmt. Eine zweite Liste
 * dessen, was es gibt, wäre eine, die irgendwann etwas anbietet, das der Canvas nicht rendern
 * kann.
 */
const props = defineProps<{
  blocks: readonly BlockDefinition[]
  categories: readonly BlockCategory[]
}>()

const groups = computed(() => paletteGroups(props.blocks, props.categories))

function startDrag(event: DragEvent, block: BlockDefinition): void {
  event.dataTransfer?.setData(
    DRAG_FORMAT,
    JSON.stringify({ kind: 'new', blockId: block.id }),
  )
  // `copy` und nicht `move`: Aus der Palette entsteht ein Exemplar, der Eintrag bleibt.
  if (event.dataTransfer) {
    event.dataTransfer.effectAllowed = 'copy'
  }
}
</script>

<template>
  <aside class="composer-palette">
    <h2>Blöcke</h2>

    <!--
      Leer heißt hier fast immer: Die Bundles der Fläche sind nicht geladen. Das steht als
      eigener Hinweis über dem Canvas; hier den Grund zu wiederholen, hieße ihn zu raten.
    -->
    <p v-if="groups.length === 0" class="composer-palette__empty">
      Keine Blöcke verfügbar.
    </p>

    <section v-for="group in groups" :key="group.categoryId" class="composer-palette__group">
      <h3>{{ group.label }}</h3>
      <ul>
        <li
          v-for="block in group.blocks"
          :key="block.id"
          class="composer-palette__block"
          draggable="true"
          :data-block-id="block.id"
          :title="block.description"
          @dragstart="startDrag($event, block)"
        >
          {{ block.label }}
        </li>
      </ul>
    </section>
  </aside>
</template>
