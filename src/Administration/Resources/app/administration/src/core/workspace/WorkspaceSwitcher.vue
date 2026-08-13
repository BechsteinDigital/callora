<template>
  <div v-if="canSwitch" class="ws-switcher">
    <CalIcon class="ws-switcher__icon" :icon="Boxes" size="sm" />
    <CalSelect
      :model-value="activeWorkspace"
      name="active-workspace"
      size="sm"
      aria-label="Aktiver Workspace"
      @update:model-value="setActive"
    >
      <option v-for="w in workspaces" :key="w.workspaceKey" :value="w.workspaceKey">
        {{ w.displayName }}
      </option>
    </CalSelect>
  </div>

  <!--
    Ohne diesen Zweig blendete ein gescheiterter Ladevorgang den Umschalter einfach aus: canSwitch
    haengt an workspaces.length > 0, und die Topbar sah aus, als haette dieser Operator schlicht
    keine Auswahl (#291). Ein sichtbares „nicht geladen" ist die einzige Variante, aus der jemand
    schliessen kann, dass etwas fehlt.
  -->
  <div v-else-if="loadFailed" class="ws-switcher ws-switcher--failed" title="Die Workspace-Liste konnte nicht geladen werden. Neu laden versucht es erneut.">
    <CalIcon class="ws-switcher__icon" :icon="Boxes" size="sm" />
    <span class="ws-switcher__failed-label">Workspaces nicht geladen</span>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { Boxes } from 'lucide-vue-next'
import CalIcon from '@/core/ui/CalIcon.vue'
import CalSelect from '@/core/ui/CalSelect.vue'
import { useWorkspaceContext } from './workspaceContext'

// The global active-workspace switcher, mounted in the topbar. It only renders
// for an operator who has workspaces to choose from; a workspace-bound admin has
// a fixed context and sees nothing here.
const { workspaces, activeWorkspace, canSwitch, ensure, setActive } = useWorkspaceContext()
const loadFailed = ref(false)

// ensureLoaded wirft bewusst weiter, damit ein spaeterer Aufruf es erneut versuchen kann
// (workspaceContext.ts). onMounted(ensure) nahm die Rejection nicht entgegen — sie lief ins
// Leere, und uebrig blieb eine Topbar ohne Umschalter.
onMounted(() => {
  void ensure().catch((error: unknown) => {
    loadFailed.value = true
    console.error('[callora-admin] workspace list could not be loaded.', error)
  })
})
</script>

<style scoped lang="scss">
.ws-switcher--failed {
  color: var(--cal-text-subtle);
  font-size: var(--cal-font-size-sm);
}

.ws-switcher {
  display: flex;
  align-items: center;
  gap: var(--cal-space-2);
  padding-left: var(--cal-space-2);
  border-left: 1px solid var(--cal-border-subtle);
}

.ws-switcher__icon {
  color: var(--cal-text-muted);
}

.ws-switcher :deep(.cal-select) {
  width: auto;
  min-width: 140px;
  max-width: 200px;
  border-color: transparent;
  background: transparent;
}

.ws-switcher :deep(.cal-select:hover) {
  border-color: var(--cal-border);
  background: var(--cal-surface-inset);
}

@media (width <= 900px) {
  .ws-switcher {
    display: none;
  }
}
</style>
