<script setup lang="ts">
import { h, resolveComponent } from 'vue';
import type { TableColumn } from '@nuxt/ui';

type BackendUserSummary = {
  externalId: string;
  email: string | null;
  displayName: string | null;
};

type WorkspaceSummary = {
  workspaceKey: string;
  displayName: string;
  isActive: boolean;
};

type TenantSummary = {
  tenantKey: string;
  displayName: string;
  isActive: boolean;
};

type PluginInstallationSummary = {
  pluginId: string;
  isActive?: boolean;
};

type RbacRoleSummary = {
  role: string;
  permissions: string[];
};

type ThemeDefinitionSummary = {
  templateKey: string;
  displayName: string;
  isActive: boolean;
};

type ModuleRow = {
  module: string;
  endpoint: string;
  status: 'ready' | 'pending' | 'disabled';
  details: string;
};

const auth = useAdminAuth();
const runtimeConfig = useRuntimeConfig();
const { requestSafe } = useAdminApi();
const { listResolvedWidgets } = useAdminWidgets();

const loading = ref(true);
const refreshedAt = ref<Date | null>(null);

const users = ref<BackendUserSummary[]>([]);
const workspaces = ref<WorkspaceSummary[]>([]);
const tenants = ref<TenantSummary[] | null>(null);
const plugins = ref<PluginInstallationSummary[]>([]);
const roles = ref<RbacRoleSummary[]>([]);
const themes = ref<ThemeDefinitionSummary[]>([]);

const displayName = computed(() => auth.session.value?.displayName || auth.session.value?.email || auth.session.value?.userId || 'Admin');

const activeWorkspaces = computed(() => workspaces.value.filter((workspace) => workspace.isActive).length);
const activeTenants = computed(() => (tenants.value || []).filter((tenant) => tenant.isActive).length);
const activePlugins = computed(() => plugins.value.filter((plugin) => plugin.isActive !== false).length);
const activeThemes = computed(() => themes.value.filter((theme) => theme.isActive).length);
const tenantApiEnabled = computed(() => runtimeConfig.public.enableTenantManagementApi === true);
const dashboardWidgets = listResolvedWidgets('dashboard.main');

const stats = computed(() => [{
  title: 'Users',
  icon: 'i-lucide-users',
  value: users.value.length.toString(),
  variation: roles.value.length > 0 ? `+${roles.value.length} roles` : 'No roles'
}, {
  title: 'Workspaces',
  icon: 'i-lucide-layout-grid',
  value: `${activeWorkspaces.value}/${workspaces.value.length}`,
  variation: 'active / total'
}, {
  title: 'Plugins',
  icon: 'i-lucide-plug-zap',
  value: `${activePlugins.value}/${plugins.value.length}`,
  variation: 'active / installed'
}, {
  title: 'Themes',
  icon: 'i-lucide-palette',
  value: `${activeThemes.value}/${themes.value.length}`,
  variation: 'active / definitions'
}]);

const rows = computed<ModuleRow[]>(() => [{
  module: 'Users',
  endpoint: 'GET /api/users',
  status: users.value.length > 0 ? 'ready' : 'pending',
  details: `${users.value.length} accounts`
}, {
  module: 'Workspaces',
  endpoint: 'GET /api/workspaces',
  status: workspaces.value.length > 0 ? 'ready' : 'pending',
  details: `${activeWorkspaces.value}/${workspaces.value.length} active`
}, {
  module: 'RBAC',
  endpoint: 'GET /api/security/rbac/roles',
  status: roles.value.length > 0 ? 'ready' : 'pending',
  details: `${roles.value.length} role definitions`
}, {
  module: 'Plugins',
  endpoint: 'GET /api/plugins/installed',
  status: plugins.value.length > 0 ? 'ready' : 'pending',
  details: `${activePlugins.value}/${plugins.value.length} active`
}, {
  module: 'Themes',
  endpoint: 'GET /api/themes/definitions',
  status: themes.value.length > 0 ? 'ready' : 'pending',
  details: `${activeThemes.value}/${themes.value.length} active`
}, {
  module: 'Tenants',
  endpoint: 'GET /api/tenants',
  status: !tenantApiEnabled.value
    ? 'disabled'
    : tenants.value && tenants.value.length > 0
      ? 'ready'
      : 'pending',
  details: !tenantApiEnabled.value
    ? 'feature disabled'
    : tenants.value
    ? `${activeTenants.value}/${tenants.value.length} active`
    : 'optional endpoint'
}]);

const UBadge = resolveComponent('UBadge');

const columns: TableColumn<ModuleRow>[] = [{
  accessorKey: 'module',
  header: 'Module'
}, {
  accessorKey: 'endpoint',
  header: 'Endpoint'
}, {
  accessorKey: 'status',
  header: 'Status',
  cell: ({ row }) => {
    const status = row.getValue('status') as ModuleRow['status'];
    const color = status === 'ready' ? 'success' : status === 'disabled' ? 'neutral' : 'warning';
    return h(UBadge, { variant: 'subtle', color, class: 'capitalize' }, () => status);
  }
}, {
  accessorKey: 'details',
  header: 'Details'
}];

async function loadDashboard(): Promise<void> {
  loading.value = true;

  const [usersResult, workspacesResult, pluginsResult, rolesResult, themesResult] = await Promise.all([
    requestSafe<BackendUserSummary[]>('/api/users'),
    requestSafe<WorkspaceSummary[]>('/api/workspaces'),
    requestSafe<PluginInstallationSummary[]>('/api/plugins/installed'),
    requestSafe<RbacRoleSummary[]>('/api/security/rbac/roles'),
    requestSafe<ThemeDefinitionSummary[]>('/api/themes/definitions')
  ]);

  users.value = usersResult.ok ? (usersResult.data ?? []) : [];
  workspaces.value = workspacesResult.ok ? (workspacesResult.data ?? []) : [];
  plugins.value = pluginsResult.ok ? (pluginsResult.data ?? []) : [];
  roles.value = rolesResult.ok ? (rolesResult.data ?? []) : [];
  themes.value = themesResult.ok ? (themesResult.data ?? []) : [];

  if (tenantApiEnabled.value) {
    const tenantsResult = await requestSafe<TenantSummary[]>('/api/tenants');
    tenants.value = tenantsResult.ok ? (tenantsResult.data ?? []) : null;
  } else {
    tenants.value = null;
  }

  refreshedAt.value = new Date();
  loading.value = false;
}

async function signOut(): Promise<void> {
  await auth.logout();
  await navigateTo({ name: 'login' });
}

await loadDashboard();
</script>

<template>
  <UDashboardPanel id="home">
    <template #header>
      <UDashboardNavbar
        title="Home"
        :ui="{ right: 'gap-3' }"
      >
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>

        <template #right>
          <UButton
            color="neutral"
            variant="ghost"
            icon="i-lucide-refresh-cw"
            :loading="loading"
            @click="loadDashboard"
          />

          <UBadge
            color="neutral"
            variant="subtle"
            class="hidden md:inline-flex"
          >
            {{ displayName }}
          </UBadge>

          <UButton
            color="neutral"
            variant="solid"
            icon="i-lucide-log-out"
            @click="signOut"
          />
        </template>
      </UDashboardNavbar>

      <UDashboardToolbar>
        <template #left>
          <UButton
            color="neutral"
            variant="ghost"
            icon="i-lucide-calendar-days"
            class="-ms-1"
          >
            {{ refreshedAt ? refreshedAt.toLocaleString() : 'No refresh yet' }}
          </UButton>
        </template>
      </UDashboardToolbar>
    </template>

    <template #body>
      <UPageGrid class="lg:grid-cols-4 gap-4 sm:gap-6 lg:gap-px">
        <UPageCard
          v-for="(stat, index) in stats"
          :key="index"
          :icon="stat.icon"
          :title="stat.title"
          variant="subtle"
          :ui="{
            container: 'gap-y-1.5',
            wrapper: 'items-start',
            leading: 'p-2.5 rounded-full bg-primary/10 ring ring-inset ring-primary/25',
            title: 'font-normal text-muted text-xs uppercase'
          }"
          class="lg:rounded-none first:rounded-l-lg last:rounded-r-lg hover:z-1"
        >
          <div class="flex items-center gap-2">
            <span class="text-2xl font-semibold text-highlighted">{{ stat.value }}</span>
            <UBadge
              color="neutral"
              variant="subtle"
              class="text-xs"
            >
              {{ stat.variation }}
            </UBadge>
          </div>
        </UPageCard>
      </UPageGrid>

      <UCard>
        <template #header>
          <div>
            <p class="text-xs text-muted uppercase mb-1.5">Platform Modules</p>
            <p class="text-3xl text-highlighted font-semibold">Admin API Overview</p>
          </div>
        </template>

        <UTable
          :data="rows"
          :columns="columns"
          class="shrink-0"
          :ui="{
            base: 'table-fixed border-separate border-spacing-0',
            thead: '[&>tr]:bg-elevated/50 [&>tr]:after:content-none',
            tbody: '[&>tr]:last:[&>td]:border-b-0',
            th: 'first:rounded-l-lg last:rounded-r-lg border-y border-default first:border-l last:border-r',
            td: 'border-b border-default'
          }"
        />
      </UCard>

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
    </template>
  </UDashboardPanel>
</template>
