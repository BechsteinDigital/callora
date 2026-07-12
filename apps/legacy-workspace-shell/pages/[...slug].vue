<script setup lang="ts">
const route = useRoute();
</script>

<template>
  <UDashboardPanel id="workspace-fallback">
    <template #header>
      <UDashboardNavbar title="Workspace Route" />
    </template>

    <template #body>
      <UEmpty
        icon="i-lucide-route"
        title="Route not mapped"
        :description="`No workspace view is registered for '${route.path}'.`"
      />
    </template>
  </UDashboardPanel>
</template>
