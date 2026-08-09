<template>
  <CalCard class="tree" flush>
    <header class="tree__header">
      <h2 class="tree__title">Flächen</h2>
      <CalButton v-if="canManage" variant="ghost" size="sm" :icon="Plus" icon-only title="Neue Fläche" @click="emit('create', null)" />
    </header>

    <div v-if="loading" class="tree__body">
      <CalSkeleton v-for="n in 4" :key="n" height="28px" />
    </div>

    <ul v-else class="tree__body" role="tree">
      <li v-for="row in rows" :key="row.surface.surfaceKey" role="none">
        <div
          class="tree__row"
          :class="{ 'is-selected': row.surface.surfaceKey === selectedKey, 'is-inactive': !row.surface.isActive }"
          :style="{ '--depth': row.depth }"
          role="treeitem"
          :aria-level="row.depth + 1"
          :aria-selected="row.surface.surfaceKey === selectedKey"
          tabindex="0"
          @click="emit('select', row.surface.surfaceKey)"
          @keydown.enter="emit('select', row.surface.surfaceKey)"
          @keydown.space.prevent="emit('select', row.surface.surfaceKey)"
        >
          <CalIcon class="tree__icon" :icon="iconFor(row)" size="sm" />
          <span class="tree__label">{{ row.surface.displayName || row.surface.surfaceKey }}</span>

          <!-- Der Auge-Knopf öffnet die ÖFFENTLICHE Adresse. Die Vorschau ist das, was einen
               Baum von einer Liste unterscheidet: Man sieht sofort, ob das Segment stimmt. -->
          <a
            v-if="publicUrlOf(row.surface)"
            class="tree__action"
            :href="publicUrlOf(row.surface)!"
            target="_blank"
            rel="noopener"
            :title="`${publicUrlOf(row.surface)} öffnen`"
            @click.stop
          >
            <CalIcon :icon="Eye" size="sm" />
          </a>
          <button
            v-if="canManage"
            type="button"
            class="tree__action"
            title="Unterseite anlegen"
            @click.stop="emit('create', row.surface.surfaceKey)"
          >
            <CalIcon :icon="Plus" size="sm" />
          </button>
        </div>
      </li>
    </ul>
  </CalCard>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { AppWindow, Eye, FileText, Folder, Plus } from 'lucide-vue-next'
import CalButton from '@/core/ui/CalButton.vue'
import CalCard from '@/core/ui/CalCard.vue'
import CalIcon from '@/core/ui/CalIcon.vue'
import CalSkeleton from '@/core/ui/CalSkeleton.vue'
import { flattenSurfaceTree, type SurfaceTreeRow } from '@/modules/workspaces/surfaceTree'
import type { WorkspaceSurface } from '@/modules/workspaces/workspacesApi'

const props = defineProps<{
  surfaces: readonly WorkspaceSurface[]
  loading: boolean
  selectedKey: string | null
  canManage: boolean
}>()

const emit = defineEmits<{
  select: [surfaceKey: string]
  create: [parentSurfaceKey: string | null]
  reload: []
}>()

const rows = computed(() => flattenSurfaceTree(props.surfaces))

const hasChildren = computed(() => {
  const parents = new Set<string>()
  for (const surface of props.surfaces) {
    if (surface.parentSurfaceKey) {
      parents.add(surface.parentSurfaceKey)
    }
  }
  return parents
})

/**
 * Das Symbol sagt, was der Knoten IST — nicht, ob er gerade aufgeklappt ist.
 *
 * Eine Anwendung sieht anders aus als eine Seite, weil sich ihr Verhalten unterscheidet: Was
 * unter ihr liegt, entsteht zur Laufzeit und steht in keinem Baum (ADR-022). Das im Baum
 * sichtbar zu machen erspart die Frage, warum dort nichts steht.
 */
function iconFor(row: SurfaceTreeRow) {
  if (row.surface.routing === 'Application') {
    return AppWindow
  }
  return hasChildren.value.has(row.surface.surfaceKey) ? Folder : FileText
}

/**
 * Die öffentliche Adresse, so weit sie sich hier ableiten lässt.
 *
 * Nur der eigene Host und das eigene Segment stehen im Datensatz — die vollständige Kette
 * berechnet der Server (ADR-021). Deshalb wird ein Kind ohne eigenen Host nicht verlinkt: Ein
 * Link, der auf die falsche Ebene zeigt, ist schlechter als keiner.
 */
function publicUrlOf(surface: WorkspaceSurface): string | null {
  if (surface.publicHost) {
    return `//${surface.publicHost}${surface.publicPathPrefix.startsWith('/') ? surface.publicPathPrefix : `/${surface.publicPathPrefix}`}`
  }
  return surface.parentSurfaceKey ? null : `/${surface.workspaceKey}`
}
</script>

<style scoped lang="scss">
.tree__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--cal-space-2);
  padding: var(--cal-space-3);
  border-bottom: 1px solid var(--cal-border-subtle);
}

.tree__title {
  margin: 0;
  font-size: var(--cal-text-sm);
  font-weight: var(--cal-weight-semibold);
  text-transform: uppercase;
  letter-spacing: var(--cal-tracking-wide);
  color: var(--cal-text-muted);
}

.tree__body {
  display: flex;
  flex-direction: column;
  gap: 1px;
  margin: 0;
  padding: var(--cal-space-2);
  list-style: none;
  max-height: calc(100vh - 220px);
  overflow-y: auto;
}

.tree__row {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
  /* Die Einrückung trägt die Struktur — ohne sie wäre der Baum eine Liste. */
  padding-inline-start: calc(var(--cal-space-2) + var(--depth) * var(--cal-space-4));
  padding-inline-end: var(--cal-space-1);
  height: 30px;
  border-radius: var(--cal-radius-sm);
  color: var(--cal-text-secondary);
  font-size: var(--cal-text-md);
  cursor: pointer;
}

.tree__row:hover {
  background: var(--cal-surface-hover);
  color: var(--cal-text);
}

.tree__row.is-selected {
  background: var(--cal-accent-subtle);
  color: var(--cal-accent);
}

/* Eine inaktive Fläche ist nicht erreichbar — sie auszublenden versteckte den Grund. */
.tree__row.is-inactive .tree__label {
  opacity: 0.55;
  text-decoration: line-through;
}

.tree__icon {
  flex: none;
  color: var(--cal-text-muted);
}

.tree__row.is-selected .tree__icon {
  color: var(--cal-accent);
}

.tree__label {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.tree__action {
  display: flex;
  align-items: center;
  flex: none;
  padding: var(--cal-space-1);
  border: 0;
  border-radius: var(--cal-radius-sm);
  background: none;
  color: var(--cal-text-muted);
  cursor: pointer;
  opacity: 0;
}

.tree__row:hover .tree__action,
.tree__row:focus-within .tree__action {
  opacity: 1;
}

.tree__action:hover {
  background: var(--cal-surface);
  color: var(--cal-text);
  text-decoration: none;
}
</style>
