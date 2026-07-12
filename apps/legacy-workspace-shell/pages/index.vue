<script setup lang="ts">
const auth = useWorkspaceAuth();
const { requestSafe } = useWorkspaceApi();
const { banners } = useWorkspaceInfoBanners();
const { listResolvedWidgets } = useWorkspaceWidgets();
const { workspaceKey, workspaceName, workspaceType, publicPathPrefix, hydrateFromPublicContext } = useWorkspaceContext();

const loading = ref(true);
const apiReachable = ref<boolean | null>(null);
const resolvedByLookup = ref(false);

const dashboardWidgets = listResolvedWidgets("dashboard.main");
const contentWidgets = listResolvedWidgets("content.main");
const sidebarWidgets = listResolvedWidgets("sidebar.main");
const userLabel = computed(() =>
  auth.session.value?.displayName ||
  auth.session.value?.email ||
  auth.session.value?.userId ||
  "Workspace User"
);

const cards = computed(() => [{
  label: "Workspace",
  value: workspaceName.value || "Unknown"
}, {
  label: "Type",
  value: workspaceType.value || "base"
}, {
  label: "User",
  value: userLabel.value
}, {
  label: "Public Prefix",
  value: publicPathPrefix.value
}]);

const extensionCards = computed(() => [{
  label: "Banners",
  value: String(banners.value.length)
}, {
  label: "Dashboard Widgets",
  value: String(dashboardWidgets.value.length)
}, {
  label: "Content Widgets",
  value: String(contentWidgets.value.length)
}, {
  label: "Sidebar Widgets",
  value: String(sidebarWidgets.value.length)
}]);

const hasExtensions = computed(() =>
  banners.value.length > 0 ||
  dashboardWidgets.value.length > 0 ||
  contentWidgets.value.length > 0 ||
  sidebarWidgets.value.length > 0
);

async function loadOverview(): Promise<void> {
  loading.value = true;
  try {
    if (!workspaceKey.value) {
      resolvedByLookup.value = await hydrateFromPublicContext("/");
    }

    const health = await requestSafe<{ status: string }>("/health");
    apiReachable.value = health.ok && (health.data?.status || "").toLowerCase() === "ok";
  } finally {
    loading.value = false;
  }
}

await loadOverview();
</script>

<template>
  <UDashboardPanel id="workspace-overview">
    <template #header>
      <UDashboardNavbar title="Workspace Overview">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>

        <template #right>
          <UButton
            color="neutral"
            variant="ghost"
            icon="i-lucide-refresh-cw"
            :loading="loading"
            @click="loadOverview"
          />
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <div class="space-y-6">
        <UPageCard :title="workspaceName">
          <template #title>
            {{ workspaceName }}
          </template>
          <template #description>
            Workspace key <code>{{ workspaceKey || "unresolved" }}</code> is online. Baseline UX stays operational even without dialer plugins.
          </template>
        </UPageCard>

        <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
          <UCard v-for="card in cards" :key="card.label">
            <p class="text-sm text-muted mb-1">{{ card.label }}</p>
            <p class="font-semibold">{{ card.value }}</p>
          </UCard>
        </div>

        <UPageCard title="Runtime Status">
          <div class="space-y-3">
            <UAlert
              v-if="apiReachable === true"
              color="success"
              variant="subtle"
              icon="i-lucide-circle-check"
              title="Control plane reachable"
              description="Workspace shell can reach backend APIs."
            />

            <UAlert
              v-else-if="apiReachable === false"
              color="warning"
              variant="soft"
              icon="i-lucide-circle-alert"
              title="Control plane not reachable"
              description="Backend APIs could not be reached from the workspace shell."
            />

            <UAlert
              v-if="resolvedByLookup"
              color="neutral"
              variant="soft"
              icon="i-lucide-route"
              title="Workspace context refreshed"
              description="Workspace context was resolved from public routing."
            />
          </div>
        </UPageCard>

        <UPageCard title="Extension Surface">
          <div class="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-3">
            <UCard v-for="entry in extensionCards" :key="entry.label">
              <p class="text-xs uppercase text-muted mb-1">{{ entry.label }}</p>
              <p class="text-xl font-semibold">{{ entry.value }}</p>
            </UCard>
          </div>
        </UPageCard>

        <UPageCard title="Current State">
          <UAlert
            v-if="hasExtensions"
            color="success"
            variant="subtle"
            icon="i-lucide-plug-zap"
            title="Workspace extensions loaded"
            description="One or more plugin extensions are active for this workspace."
          />

          <UAlert
            v-else
            color="neutral"
            variant="subtle"
            icon="i-lucide-info"
            title="Baseline mode active"
            description="No dialer or plugin workspace modules are active yet. This is the intended default state."
          />
        </UPageCard>
      </div>
    </template>
  </UDashboardPanel>
</template>
