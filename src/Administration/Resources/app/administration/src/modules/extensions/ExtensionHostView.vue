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
          title="Dieses Plugin liefert keine eigene Admin-Oberfläche."
          description="Seine Funktionen sind über die Plugin-API erreichbar. Eine Oberfläche erscheint hier, sobald das Plugin ein Admin-Bundle mitliefert."
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

onMounted(ensureWorkspace)
</script>

<style scoped lang="scss">
.extension-host {
  min-width: 0;
}
</style>
