<script setup lang="ts">
const { requestSafe } = useWorkspaceApi();
const { banners } = useWorkspaceInfoBanners();
const { listResolvedWidgets } = useWorkspaceWidgets();
const { workspaceKey, workspaceName, workspaceType } = useWorkspaceContext();

const blockContext = computed(() => ({ workspaceKey: workspaceKey.value }));

const hasPluginWidgets = computed(() => banners.value.length > 0);
const dashboardWidgets = listResolvedWidgets("dashboard.main");
const loading = ref(true);
const apiReachable = ref<boolean | null>(null);

const statusCards = computed(() => [{
  label: "Workspace Key",
  value: workspaceKey.value || "unresolved"
}, {
  label: "Workspace Type",
  value: workspaceType.value
}, {
  label: "Plugin Banners",
  value: String(banners.value.length)
}, {
  label: "Dashboard Widgets",
  value: String(dashboardWidgets.value.length)
}]);

async function loadDashboardStatus(): Promise<void> {
  loading.value = true;
  try {
    const health = await requestSafe<{ status: string }>("/health");
    apiReachable.value = health.ok && (health.data?.status || "").toLowerCase() === "ok";
  } finally {
    loading.value = false;
  }
}

await loadDashboardStatus();
</script>

<template>
  <UDashboardPanel id="workspace-dashboard">
    <template #header>
      <UDashboardNavbar title="Dashboard">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>

        <template #right>
          <UButton
            color="neutral"
            variant="ghost"
            icon="i-lucide-refresh-cw"
            :loading="loading"
            @click="loadDashboardStatus"
          />
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <div class="space-y-6">
        <ShellBlock name="workspace.dashboard.before" :context="blockContext" />

        <UPageCard :title="`Workspace Dashboard • ${workspaceName}`">
          <template #description>
            Baseline dashboard with empty and degraded states, ready for plugin enrichments.
          </template>
        </UPageCard>

        <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
          <UCard v-for="card in statusCards" :key="card.label">
            <p class="text-xs uppercase text-muted mb-1">{{ card.label }}</p>
            <p class="text-lg font-semibold">{{ card.value }}</p>
          </UCard>
        </div>

        <UAlert
          v-if="apiReachable === false"
          color="warning"
          variant="soft"
          icon="i-lucide-circle-alert"
          title="Backend currently unreachable"
          description="Workspace shell could not reach backend APIs. Plugin widgets may show stale data."
        />

        <UPageCard v-if="hasPluginWidgets" title="Plugin Signals">
          <ul class="space-y-2">
            <li v-for="banner in banners" :key="banner.id" class="border border-default rounded-lg p-3">
              <p class="font-medium">{{ banner.title }}</p>
              <p class="text-sm text-muted">{{ banner.description || "No description." }}</p>
            </li>
          </ul>
        </UPageCard>

        <UEmpty
          v-else
          icon="i-lucide-layout-grid"
          title="No workspace widgets active yet"
          description="No plugin-provided dashboard widgets are enabled for this workspace. Baseline dashboard remains available."
        />

        <div v-if="dashboardWidgets.length > 0" class="grid grid-cols-1 xl:grid-cols-2 gap-4">
          <UCard v-for="widget in dashboardWidgets" :key="`${widget.pluginId}:${widget.widgetKey}`">
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

        <ShellBlock name="workspace.dashboard.after" :context="blockContext" />
      </div>
    </template>
  </UDashboardPanel>
</template>
