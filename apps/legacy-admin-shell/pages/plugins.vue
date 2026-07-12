<script setup lang="ts">
import { h, resolveComponent } from 'vue';
import type { DropdownMenuItem, TableColumn } from '@nuxt/ui';
import type {
  PluginAuditEntry,
  PluginContractCompatibility,
  PluginContractSupport,
  PluginInstallationSummary,
  PluginLifecycleApiResponse,
  TrustedPluginSigner
} from '~/types/admin-plugins';
import type { AdminTenant, AdminWorkspace } from '~/types/admin-workspaces';

type LifecycleAction = 'activate' | 'deactivate' | 'uninstall' | 'install' | 'update-local';

const auth = useAdminAuth();
const { request, requestSafe } = useAdminApi();
const { listResolvedWidgets } = useAdminWidgets();

const plugins = ref<PluginInstallationSummary[]>([]);
const runtimePlugins = ref<Array<Record<string, unknown>>>([]);
const pluginAuditEntries = ref<PluginAuditEntry[]>([]);
const contractSupport = ref<PluginContractSupport[]>([]);
const contractCompatibility = ref<PluginContractCompatibility[]>([]);
const trustedSigners = ref<TrustedPluginSigner[]>([]);

const loading = ref(true);
const diagnosticsLoading = ref(true);
const refreshedAt = ref<Date | null>(null);
const listError = ref<string | null>(null);
const diagnosticsError = ref<string | null>(null);

const workspaces = ref<AdminWorkspace[]>([]);
const tenants = ref<AdminTenant[]>([]);

const confirmInstallOpen = ref(false);
const installModalOpen = ref(false);
const updateNuGetModalOpen = ref(false);
const updateNuGetPluginId = ref('');

const filterValue = ref('');

const mutatingPluginId = ref<string | null>(null);
const mutatingAction = ref<LifecycleAction | null>(null);

const displayName = computed(() => auth.session.value?.displayName || auth.session.value?.email || auth.session.value?.userId || 'admin');
const pluginPageWidgets = listResolvedWidgets('plugins.main');

const filteredPlugins = computed(() => {
  const value = filterValue.value.trim().toLowerCase();
  if (!value) {
    return plugins.value;
  }

  return plugins.value.filter((plugin) =>
    plugin.pluginId.toLowerCase().includes(value) ||
    plugin.displayName.toLowerCase().includes(value) ||
    plugin.assemblyPath.toLowerCase().includes(value)
  );
});

const pluginOptions = computed(() => plugins.value.map((plugin) => ({
  label: plugin.displayName || plugin.pluginId,
  value: plugin.pluginId
})));

const workspaceOptions = computed(() => workspaces.value.map((workspace) => ({
  label: workspace.displayName,
  value: workspace.workspaceKey
})));

const tenantOptions = computed(() => {
  const byApi = tenants.value.map((tenant) => tenant.tenantKey);
  const fromWorkspaces = workspaces.value.map((workspace) => workspace.tenantKey);
  return [...new Set([...byApi, ...fromWorkspaces].filter((value) => value && value.trim().length > 0))]
    .map((tenantKey) => ({
      label: tenantKey,
      value: tenantKey
    }));
});

function toStateLabel(state: number): string {
  switch (state) {
    case 1:
      return 'active';
    case 2:
      return 'inactive';
    case 3:
      return 'uninstalled';
    default:
      return 'installed';
  }
}

function toStateColor(state: number): 'success' | 'warning' | 'neutral' | 'error' {
  switch (state) {
    case 1:
      return 'success';
    case 2:
      return 'warning';
    case 3:
      return 'error';
    default:
      return 'neutral';
  }
}

function toLocalDateTime(value: string): string {
  return new Date(value).toLocaleString();
}

function extractErrorMessage(error: unknown, fallback: string): string {
  const payload = (error as { data?: { message?: unknown } } | null)?.data;
  if (payload && typeof payload.message === 'string' && payload.message.trim().length > 0) {
    return payload.message;
  }

  return fallback;
}

async function loadPlugins(): Promise<void> {
  loading.value = true;
  listError.value = null;

  const response = await requestSafe<PluginInstallationSummary[]>('/api/plugins/installed');
  if (response.ok) {
    plugins.value = response.data ?? [];
  } else {
    plugins.value = [];
    listError.value = 'Plugins konnten nicht geladen werden.';
  }

  refreshedAt.value = new Date();
  loading.value = false;
}

async function loadPluginContext(): Promise<void> {
  const [workspacesResponse, tenantsResponse] = await Promise.all([
    requestSafe<AdminWorkspace[]>('/api/workspaces'),
    requestSafe<AdminTenant[]>('/api/tenants')
  ]);

  workspaces.value = workspacesResponse.ok ? (workspacesResponse.data ?? []) : [];
  tenants.value = tenantsResponse.ok ? (tenantsResponse.data ?? []) : [];
}

async function loadPluginDiagnostics(): Promise<void> {
  diagnosticsLoading.value = true;
  diagnosticsError.value = null;

  const [runtimeResponse, auditResponse, supportResponse, compatibilityResponse, trustedSignersResponse] = await Promise.all([
    requestSafe<Array<Record<string, unknown>>>('/api/plugins'),
    requestSafe<PluginAuditEntry[]>('/api/plugins/audit?take=100'),
    requestSafe<PluginContractSupport[]>('/api/plugins/contracts/support'),
    requestSafe<PluginContractCompatibility[]>('/api/plugins/contracts/compatibility'),
    requestSafe<TrustedPluginSigner[]>('/api/plugins/security/trusted-signers')
  ]);

  runtimePlugins.value = runtimeResponse.ok ? (runtimeResponse.data ?? []) : [];
  pluginAuditEntries.value = auditResponse.ok ? (auditResponse.data ?? []) : [];
  contractSupport.value = supportResponse.ok ? (supportResponse.data ?? []) : [];
  contractCompatibility.value = compatibilityResponse.ok ? (compatibilityResponse.data ?? []) : [];
  trustedSigners.value = trustedSignersResponse.ok ? (trustedSignersResponse.data ?? []) : [];

  const failedEndpoints: string[] = [];
  if (!runtimeResponse.ok) failedEndpoints.push('/api/plugins');
  if (!auditResponse.ok) failedEndpoints.push('/api/plugins/audit');
  if (!supportResponse.ok) failedEndpoints.push('/api/plugins/contracts/support');
  if (!compatibilityResponse.ok) failedEndpoints.push('/api/plugins/contracts/compatibility');
  if (!trustedSignersResponse.ok) failedEndpoints.push('/api/plugins/security/trusted-signers');

  if (failedEndpoints.length > 0) {
    diagnosticsError.value = `Einige Plugin-Endpunkte konnten nicht geladen werden: ${failedEndpoints.join(', ')}`;
  }

  diagnosticsLoading.value = false;
}

async function reloadAllData(): Promise<void> {
  await Promise.all([
    loadPlugins(),
    loadPluginContext(),
    loadPluginDiagnostics()
  ]);
}

function openInstallConfirmModal(): void {
  confirmInstallOpen.value = true;
}

function openUpdateNuGetModal(pluginId: string): void {
  updateNuGetPluginId.value = pluginId;
  updateNuGetModalOpen.value = true;
}

async function runLifecycleAction(action: LifecycleAction, pluginId: string): Promise<void> {
  listError.value = null;
  mutatingPluginId.value = pluginId;
  mutatingAction.value = action;

  try {
    let result: PluginLifecycleApiResponse;

    if (action === 'install') {
      result = await request<PluginLifecycleApiResponse>('/api/plugins/install/local', {
        method: 'POST',
        body: {
          pluginId,
          buildIfNeeded: true,
          forceBuild: false,
          requestedBy: auth.session.value?.userId || null
        }
      });
    } else if (action === 'update-local') {
      result = await request<PluginLifecycleApiResponse>(`/api/plugins/${encodeURIComponent(pluginId)}/update/local`, {
        method: 'POST',
        body: {
          buildIfNeeded: true,
          forceBuild: false,
          requestedBy: auth.session.value?.userId || null
        }
      });
    } else if (action === 'uninstall') {
      result = await request<PluginLifecycleApiResponse>(`/api/plugins/${encodeURIComponent(pluginId)}`, {
        method: 'DELETE',
        params: {
          requestedBy: auth.session.value?.userId || undefined
        }
      });
    } else {
      result = await request<PluginLifecycleApiResponse>(`/api/plugins/${encodeURIComponent(pluginId)}/${action}`, {
        method: 'POST',
        body: {
          requestedBy: auth.session.value?.userId || null,
          workspaceKey: null
        }
      });
    }

    if (!result.isSuccess) {
      listError.value = result.message || `Plugin konnte nicht ${action} ausgeführt werden.`;
      return;
    }

    await reloadAllData();
  } catch (error) {
    listError.value = extractErrorMessage(error, `Plugin konnte nicht ${action} ausgeführt werden.`);
  } finally {
    mutatingPluginId.value = null;
    mutatingAction.value = null;
  }
}

function getRowItems(plugin: PluginInstallationSummary): DropdownMenuItem[] {
  const isActive = plugin.state === 1;
  const isUninstalled = plugin.state === 3;

  const lifecycleItem: DropdownMenuItem = isUninstalled
    ? {
        label: 'Install',
        icon: 'i-lucide-download',
        onSelect: () => {
          void runLifecycleAction('install', plugin.pluginId);
        }
      }
    : {
        label: isActive ? 'Deactivate' : 'Activate',
        icon: isActive ? 'i-lucide-circle-off' : 'i-lucide-power',
        onSelect: () => {
          void runLifecycleAction(isActive ? 'deactivate' : 'activate', plugin.pluginId);
        }
      };

  const items: DropdownMenuItem[] = [{
    type: 'label',
    label: plugin.pluginId
  }, lifecycleItem];

  if (!isUninstalled) {
    items.push({
      label: 'Update (Local)',
      icon: 'i-lucide-refresh-cw',
      onSelect: () => {
        void runLifecycleAction('update-local', plugin.pluginId);
      }
    });

    items.push({
      label: 'Update (NuGet)',
      icon: 'i-lucide-refresh-cw',
      onSelect: () => {
        openUpdateNuGetModal(plugin.pluginId);
      }
    });
  }

  items.push({
    type: 'separator'
  }, {
    label: 'Uninstall',
    icon: 'i-lucide-trash',
    color: 'error',
    onSelect: () => {
      void runLifecycleAction('uninstall', plugin.pluginId);
    }
  });

  return items;
}

const UBadge = resolveComponent('UBadge');
const UButton = resolveComponent('UButton');
const UDropdownMenu = resolveComponent('UDropdownMenu');

const columns: TableColumn<PluginInstallationSummary>[] = [{
  accessorKey: 'pluginId',
  header: 'Plugin'
}, {
  accessorKey: 'displayName',
  header: 'Name'
}, {
  accessorKey: 'state',
  header: 'Status',
  cell: ({ row }) => {
    const state = Number(row.getValue('state'));
    return h(UBadge, {
      variant: 'subtle',
      color: toStateColor(state),
      class: 'capitalize'
    }, () => toStateLabel(state));
  }
}, {
  accessorKey: 'assemblyPath',
  header: 'Assembly'
}, {
  accessorKey: 'updatedAtUtc',
  header: 'Updated',
  cell: ({ row }) => toLocalDateTime(row.original.updatedAtUtc)
}, {
  id: 'actions',
  header: '',
  cell: ({ row }) => h('div', { class: 'text-right' }, [h(UDropdownMenu, {
    content: { align: 'end' },
    items: getRowItems(row.original)
  }, () => h(UButton, {
    color: 'neutral',
    variant: 'ghost',
    icon: 'i-lucide-ellipsis'
  }))])
}];

await reloadAllData();
</script>

<template>
  <UDashboardPanel id="plugins">
    <template #header>
      <UDashboardNavbar title="Plugins">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>

        <template #right>
          <UButton
            color="neutral"
            variant="ghost"
            icon="i-lucide-refresh-cw"
            :loading="loading || diagnosticsLoading"
            @click="reloadAllData"
          />
          <UButton
            color="primary"
            icon="i-lucide-upload"
            @click="openInstallConfirmModal"
          >
            Erweiterung hochladen
          </UButton>
        </template>
      </UDashboardNavbar>

      <UDashboardToolbar>
        <template #left>
          <UInput
            v-model="filterValue"
            icon="i-lucide-search"
            placeholder="Plugins filtern..."
            class="w-72"
          />
        </template>

        <template #right>
          <UBadge color="neutral" variant="subtle">
            {{ refreshedAt ? refreshedAt.toLocaleString() : 'Kein Refresh' }}
          </UBadge>
          <UBadge color="neutral" variant="subtle">
            {{ displayName }}
          </UBadge>
          <UBadge color="primary" variant="subtle">
            {{ filteredPlugins.length }} Plugins
          </UBadge>
          <UBadge color="neutral" variant="subtle">
            Runtime {{ runtimePlugins.length }}
          </UBadge>
        </template>
      </UDashboardToolbar>
    </template>

    <template #body>
      <div class="space-y-6">
        <UAlert
          v-if="listError"
          color="error"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          :title="listError"
        />

        <UAlert
          v-if="diagnosticsError"
          color="warning"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          :title="diagnosticsError"
        />

        <div v-if="pluginPageWidgets.length > 0" class="grid grid-cols-1 xl:grid-cols-2 gap-4">
          <UCard v-for="widget in pluginPageWidgets" :key="`${widget.pluginId}:${widget.widgetKey}`">
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

        <UPageCard title="Installierte Plugins">
          <UTable
            :data="filteredPlugins"
            :columns="columns"
            :loading="loading"
            class="shrink-0"
            :ui="{
              base: 'table-fixed border-separate border-spacing-0',
              thead: '[&>tr]:bg-elevated/50',
              th: 'border-y border-default first:border-l last:border-r',
              td: 'border-b border-default'
            }"
          />
        </UPageCard>

        <UAlert
          v-if="mutatingPluginId"
          color="neutral"
          variant="subtle"
          icon="i-lucide-loader-circle"
          :title="`Plugin ${mutatingPluginId} wird ${mutatingAction || 'aktualisiert'}...`"
        />

        <AdminPluginWorkspaceAssignments
          :plugins="plugins"
          :workspace-options="workspaceOptions"
          @changed="loadPluginDiagnostics"
        />

        <AdminPluginGovernancePanels
          :contract-support="contractSupport"
          :contract-compatibility="contractCompatibility"
          :trusted-signers="trustedSigners"
          :audit-entries="pluginAuditEntries"
          :loading="diagnosticsLoading"
        />

        <AdminPluginEntitlementCheck
          :plugin-options="pluginOptions"
          :workspace-options="workspaceOptions"
          :tenant-options="tenantOptions"
        />
      </div>
    </template>
  </UDashboardPanel>

  <AdminPluginInstallModals
    v-model:confirm-open="confirmInstallOpen"
    v-model:install-open="installModalOpen"
    v-model:update-open="updateNuGetModalOpen"
    :update-plugin-id="updateNuGetPluginId"
    @completed="reloadAllData"
  />
</template>
