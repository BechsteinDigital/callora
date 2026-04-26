<script setup lang="ts">
const { banners } = useWorkspaceInfoBanners();
const { listResolvedWidgets } = useWorkspaceWidgets();

const sortedBanners = computed(() =>
  banners.value.slice().sort((left, right) => left.title.localeCompare(right.title))
);
const contentWidgets = listResolvedWidgets("content.main");
</script>

<template>
  <UDashboardPanel id="workspace-content">
    <template #header>
      <UDashboardNavbar title="Workspace Content" />
    </template>

    <template #body>
      <div class="space-y-6">
        <UPageCard title="Extension Surface">
          <template #description>
            Workspace plugin modules can register content blocks and banners on this surface.
          </template>
        </UPageCard>

        <UEmptyState
          v-if="sortedBanners.length === 0 && contentWidgets.length === 0"
          icon="i-lucide-plug-zap"
          title="No content extensions active"
          description="No workspace plugin has registered content blocks for this workspace."
        />

        <div v-if="sortedBanners.length > 0" class="grid grid-cols-1 lg:grid-cols-2 gap-4">
          <UCard v-for="banner in sortedBanners" :key="banner.id">
            <template #header>
              <div class="flex items-center justify-between gap-2">
                <span class="font-semibold">{{ banner.title }}</span>
                <UBadge color="neutral" variant="subtle">
                  {{ banner.pluginId || "workspace" }}
                </UBadge>
              </div>
            </template>
            <p class="text-sm text-muted">
              {{ banner.description || "No description provided." }}
            </p>
          </UCard>
        </div>

        <div v-if="contentWidgets.length > 0" class="grid grid-cols-1 lg:grid-cols-2 gap-4">
          <UCard v-for="widget in contentWidgets" :key="`${widget.pluginId}:${widget.widgetKey}`">
            <template #header>
              <div class="flex items-center justify-between gap-2">
                <span class="font-semibold">{{ widget.title }}</span>
                <UBadge color="neutral" variant="subtle">
                  {{ widget.pluginId }}
                </UBadge>
              </div>
            </template>

            <p v-if="widget.description" class="text-sm text-muted mb-3">
              {{ widget.description }}
            </p>
            <div v-if="widget.contentHtml" class="text-sm" v-html="widget.contentHtml" />
          </UCard>
        </div>
      </div>
    </template>
  </UDashboardPanel>
</template>
