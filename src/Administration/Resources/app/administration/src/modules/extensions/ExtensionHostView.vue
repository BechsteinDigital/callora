<template>
  <div class="extension-host">
    <template v-if="pages.length">
      <component :is="page" v-for="(page, index) in pages" :key="index" :ctx="pageContext" />
    </template>
    <CalPage v-else>
      <CalPageHeader :title="pluginId" />
      <CalCard>
        <CalEmptyState
          :icon="Puzzle"
          :title="emptyState.title"
          :description="emptyState.description"
        />
      </CalCard>
    </CalPage>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { Puzzle } from 'lucide-vue-next'
import { getExtensions } from '@/core/extensions/registry'
import { useWorkspaceContext } from '@/core/workspace/workspaceContext'
import CalCard from '@/core/ui/CalCard.vue'
import CalEmptyState from '@/core/ui/CalEmptyState.vue'
import CalPage from '@/core/ui/CalPage.vue'
import CalPageHeader from '@/core/ui/CalPageHeader.vue'

// Generic host for a plugin's admin page. The route is neutral: it renders
// whatever component a plugin registered for its page slot (via a UI bundle),
// or a graceful placeholder when the plugin ships no admin UI. The shell stays
// domain-neutral — no plugin-specific code here.
//
// A plugin page is NOT wrapped in CalPage: the plugin owns its full canvas and
// decides its own framing, exactly as it would in a standalone route.
const route = useRoute()
const pluginId = computed(() => String(route.params.pluginId ?? ''))
const { activeWorkspace, ensure: ensureWorkspace } = useWorkspaceContext()
const pageContext = computed(() => ({
  workspaceKey: activeWorkspace.value.trim() || null,
}))

// Slot convention for a plugin's full admin page — a stable public contract.
const pages = computed(() => getExtensions(`extension.page.${pluginId.value}`))

// Ohne Seiten gibt es ZWEI Gründe, und sie zu verwechseln kostet den Betreiber Zeit: Das
// Plugin bringt keine Oberfläche mit — oder es bringt eine, sie wurde für den aktuellen
// Bereich nur nicht geladen, weil Plugin-Oberflächen an einen Workspace gebunden sind.
// Vorher behauptete die Seite immer das Erste. Auf einer frischen Installation war das
// falsch und schickte den Betreiber auf die Suche nach einem Fehler im Plugin.
const emptyState = computed(() =>
  pageContext.value.workspaceKey
    ? {
        title: 'Dieses Plugin liefert keine eigene Admin-Oberfläche.',
        description:
          'Seine Funktionen sind über die Plugin-API erreichbar. Eine Oberfläche erscheint hier, sobald das Plugin ein Admin-Bundle mitliefert.',
      }
    : {
        title: 'Kein Workspace ausgewählt.',
        description:
          'Plugin-Oberflächen sind an einen Workspace gebunden. Wähle oben einen aus — legst du gerade erst einen an, erscheint die Oberfläche danach.',
      },
)

onMounted(ensureWorkspace)
</script>

<style scoped lang="scss">
.extension-host {
  min-width: 0;
}
</style>
