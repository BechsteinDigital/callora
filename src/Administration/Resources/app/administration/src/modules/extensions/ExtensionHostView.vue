<template>
  <section class="extension-host">
    <template v-if="pages.length">
      <component :is="page" v-for="(page, index) in pages" :key="index" />
    </template>
    <div v-else class="placeholder">
      <h1>{{ pluginId }}</h1>
      <p>
        Dieses Plugin liefert (noch) keine eigene Admin-Oberfläche. Seine Funktionen
        sind über die Plugin-API erreichbar; eine UI erscheint hier, sobald das Plugin
        ein Admin-Bundle mitliefert.
      </p>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { getExtensions } from '@/core/extensions/registry'

// Generic host for a plugin's admin page. The route is neutral: it renders
// whatever component a plugin registered for its page slot (via a UI bundle),
// or a graceful placeholder when the plugin ships no admin UI. The shell stays
// domain-neutral — no plugin-specific code here.
const route = useRoute()
const pluginId = computed(() => String(route.params.pluginId ?? ''))

// Slot convention for a plugin's full admin page — a stable public contract.
const pages = computed(() => getExtensions(`extension.page.${pluginId.value}`))
</script>

<style scoped lang="scss">
.extension-host {
  padding: calc(var(--cal-space) * 2);
}

.placeholder h1 {
  text-transform: capitalize;
}

.placeholder p {
  color: var(--cal-color-text-muted);
  max-width: 60ch;
}
</style>
