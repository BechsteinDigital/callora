<template>
  <div class="surfaces">
    <SurfaceTreePanel
      class="surfaces__tree"
      :surfaces="surfaces"
      :loading="loading"
      :selected-key="selectedKey"
      :can-manage="canManage"
      @select="select"
      @create="startCreate"
      @reload="load"
    />

    <div class="surfaces__detail">
      <CalEmptyState
        v-if="!loading && surfaces.length === 0"
        :icon="Layers"
        title="Noch keine Fläche"
        description="Eine Fläche ist der Zugang zu diesem Workspace — eine Website, ein Portal, eine Anwendung."
      >
        <template #action>
          <CalButton v-if="canManage" @click="startCreate(null)">Fläche anlegen</CalButton>
        </template>
      </CalEmptyState>

      <CalEmptyState
        v-else-if="!selected && !creating"
        :icon="MousePointerClick"
        title="Nichts ausgewählt"
        description="Wählen Sie links eine Fläche oder eine Seite."
      />

      <SurfaceDetail
        v-else
        :key="selectedKey ?? '__neu__'"
        :workspace-key="workspaceKey"
        :surface="selected"
        :surfaces="surfaces"
        :parent-key="creatingParentKey"
        :can-manage="canManage"
        @saved="onSaved"
        @removed="onRemoved"
        @cancel="cancelCreate"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { Layers, MousePointerClick } from 'lucide-vue-next'
import CalButton from '@/core/ui/CalButton.vue'
import CalEmptyState from '@/core/ui/CalEmptyState.vue'
import { useAuthStore } from '@/core/auth/authStore'
import { hasPermission } from '@/core/auth/permissions'
import { useWorkspaceContext } from '@/core/workspace/workspaceContext'
import { useService } from '@/core/extensions/services'
import { workspacesApi, type WorkspaceSurface } from '@/modules/workspaces/workspacesApi'
import SurfaceDetail from './SurfaceDetail.vue'
import SurfaceTreePanel from './SurfaceTreePanel.vue'

// Der Baum steht links und bleibt stehen — auch beim Wechsel zwischen Knoten. Das ist der
// Unterschied zu einer Liste mit Detailseite: Wer eine Gliederung bearbeitet, arbeitet an den
// Beziehungen, nicht an einem Knoten nach dem anderen.
const route = useRoute()
const router = useRouter()
const api = useService('workspacesApi', workspacesApi)
const { activeWorkspace } = useWorkspaceContext()

const ctx = useAuthStore().context
const canManage = computed(() => hasPermission(ctx.value, 'workspace.update'))

const surfaces = ref<WorkspaceSurface[]>([])
const loading = ref(true)
const creating = ref(false)
const creatingParentKey = ref<string | null>(null)

const workspaceKey = computed(() => activeWorkspace.value)
const selectedKey = computed(() => (route.params.surfaceKey as string | undefined) ?? null)
const selected = computed(
  () => surfaces.value.find((surface) => surface.surfaceKey === selectedKey.value) ?? null,
)

async function load(): Promise<void> {
  if (!workspaceKey.value) {
    surfaces.value = []
    loading.value = false
    return
  }

  loading.value = true
  try {
    surfaces.value = await api.listSurfaces(workspaceKey.value)
  } finally {
    loading.value = false
  }
}

function select(surfaceKey: string): void {
  creating.value = false
  void router.push(`/surfaces/${encodeURIComponent(surfaceKey)}`)
}

function startCreate(parentKey: string | null): void {
  creatingParentKey.value = parentKey
  creating.value = true
  // Die Auswahl fällt weg: Ein neuer Knoten hat noch keinen Schlüssel, und ein URL-Segment,
  // das auf nichts zeigt, wäre beim Neuladen eine tote Adresse.
  if (selectedKey.value) {
    void router.push('/surfaces')
  }
}

function cancelCreate(): void {
  creating.value = false
  creatingParentKey.value = null
}

async function onSaved(surfaceKey: string): Promise<void> {
  creating.value = false
  creatingParentKey.value = null
  await load()
  if (surfaceKey !== selectedKey.value) {
    select(surfaceKey)
  }
}

async function onRemoved(): Promise<void> {
  await load()
  void router.push('/surfaces')
}

watch(workspaceKey, load, { immediate: true })
</script>

<style scoped lang="scss">
.surfaces {
  display: grid;
  grid-template-columns: minmax(240px, 320px) minmax(0, 1fr);
  gap: var(--cal-space-4);
  align-items: start;
}

.surfaces__tree {
  position: sticky;
  top: var(--cal-space-4);
}

.surfaces__detail {
  min-width: 0;
}

/* Unter einer gewissen Breite steht der Baum ÜBER dem Detail statt daneben. Zwei Spalten,
   von denen eine zu schmal für ihre Einrückung ist, sind schlechter als eine. */
@media (width <= 900px) {
  .surfaces {
    grid-template-columns: minmax(0, 1fr);
  }

  .surfaces__tree {
    position: static;
  }
}
</style>
