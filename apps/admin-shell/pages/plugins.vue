<script setup lang="ts">
import { h, resolveComponent } from 'vue';
import type { DropdownMenuItem, TableColumn } from '@nuxt/ui';
import type {
  InstallLocalPluginRequest,
  InstallNuGetPluginRequest,
  PluginAuditEntry,
  PluginContractCompatibility,
  PluginContractSupport,
  PluginEntitlementStatus,
  PluginInstallationSummary,
  PluginLifecycleApiResponse,
  TrustedPluginSigner
} from '~/types/admin-plugins';
import type { AdminTenant, AdminWorkspace } from '~/types/admin-workspaces';

type InstallSource = 'local' | 'nuget' | 'assembly' | 'zip';
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

const workspaceEntitlementResult = ref<PluginEntitlementStatus | null>(null);
const tenantEntitlementResult = ref<PluginEntitlementStatus | null>(null);
const entitlementError = ref<string | null>(null);
const workspaceEntitlementLoading = ref(false);
const tenantEntitlementLoading = ref(false);
const workspaceAssignmentWorkspaceKey = ref('');
const workspaceAssignmentStates = ref<Record<string, boolean>>({});
const workspaceAssignmentLoading = ref(false);
const workspaceAssignmentMutatingPluginId = ref<string | null>(null);
const workspaceAssignmentError = ref<string | null>(null);

const confirmInstallOpen = ref(false);
const installModalOpen = ref(false);
const updateNuGetModalOpen = ref(false);
const installSource = ref<InstallSource>('local');
const installError = ref<string | null>(null);
const installInfo = ref<string | null>(null);
const installPending = ref(false);
const updateNuGetError = ref<string | null>(null);
const updateNuGetPending = ref(false);

const filterValue = ref('');
const selectedZipFile = ref<File | null>(null);

const localState = reactive({
  pluginId: '',
  buildIfNeeded: true,
  forceBuild: false
});

const nugetState = reactive({
  packageId: '',
  packageVersion: '',
  assemblyFileName: '',
  entryTypeName: ''
});

const assemblyState = reactive({
  assemblyPath: '',
  entryTypeName: ''
});

const updateNuGetState = reactive({
  pluginId: '',
  packageId: '',
  packageVersion: '',
  assemblyFileName: '',
  entryTypeName: ''
});

const entitlementState = reactive({
  pluginId: '',
  workspaceKey: '',
  tenantId: ''
});

const mutatingPluginId = ref<string | null>(null);
const mutatingAction = ref<LifecycleAction | null>(null);

const installSourceOptions = [{
  label: 'Lokales Plugin',
  value: 'local'
}, {
  label: 'NuGet-Paket',
  value: 'nuget'
}, {
  label: 'Assembly-Pfad',
  value: 'assembly'
}, {
  label: 'ZIP-Datei',
  value: 'zip'
}];

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

const selectedZipFileName = computed(() => selectedZipFile.value?.name || 'Keine Datei ausgewählt');

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
  if (typeof error !== 'object' || !error) {
    return fallback;
  }

  const payload = (error as { data?: { message?: unknown } }).data;
  if (payload && typeof payload.message === 'string' && payload.message.trim().length > 0) {
    return payload.message;
  }

  return fallback;
}

function syncEntitlementDefaults(): void {
  const defaultPluginOption = pluginOptions.value[0];
  const defaultWorkspaceOption = workspaceOptions.value[0];
  const defaultTenantOption = tenantOptions.value[0];

  if (!entitlementState.pluginId && defaultPluginOption) {
    entitlementState.pluginId = defaultPluginOption.value;
  }

  if (!entitlementState.workspaceKey && defaultWorkspaceOption) {
    entitlementState.workspaceKey = defaultWorkspaceOption.value;
  }

  if (!workspaceAssignmentWorkspaceKey.value && defaultWorkspaceOption) {
    workspaceAssignmentWorkspaceKey.value = defaultWorkspaceOption.value;
  }

  if (!entitlementState.tenantId && defaultTenantOption) {
    entitlementState.tenantId = defaultTenantOption.value;
  }
}

const workspaceAssignmentRows = computed(() => plugins.value.map((plugin) => ({
  pluginId: plugin.pluginId,
  displayName: plugin.displayName,
  state: plugin.state,
  isEntitled: workspaceAssignmentStates.value[plugin.pluginId] ?? false
})));

async function loadPlugins(): Promise<void> {
  loading.value = true;
  listError.value = null;

  const response = await requestSafe<PluginInstallationSummary[]>('/api/plugins/installed');
  if (response.ok) {
    plugins.value = response.data ?? [];
    syncEntitlementDefaults();
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
  syncEntitlementDefaults();
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

  await loadWorkspaceAssignments();
}

async function loadWorkspaceAssignments(): Promise<void> {
  workspaceAssignmentError.value = null;
  workspaceAssignmentStates.value = {};

  const selectedWorkspaceKey = workspaceAssignmentWorkspaceKey.value.trim();
  if (!selectedWorkspaceKey || plugins.value.length === 0) {
    return;
  }

  workspaceAssignmentLoading.value = true;
  const failures: string[] = [];
  const entries = await Promise.all(plugins.value.map(async (plugin) => {
    const response = await requestSafe<PluginEntitlementStatus>(
      `/api/plugins/workspaces/${encodeURIComponent(selectedWorkspaceKey)}/entitlements/${encodeURIComponent(plugin.pluginId)}`
    );

    if (!response.ok || !response.data) {
      failures.push(plugin.pluginId);
      return [plugin.pluginId, false] as const;
    }

    return [plugin.pluginId, response.data.isEntitled] as const;
  }));

  workspaceAssignmentStates.value = Object.fromEntries(entries);
  if (failures.length > 0) {
    workspaceAssignmentError.value = `Workspace-Zuweisung konnte nicht vollständig geladen werden: ${failures.join(', ')}`;
  }
  workspaceAssignmentLoading.value = false;
}

async function setWorkspacePluginAssignment(pluginId: string, isEnabled: boolean): Promise<void> {
  workspaceAssignmentError.value = null;
  const selectedWorkspaceKey = workspaceAssignmentWorkspaceKey.value.trim();
  if (!selectedWorkspaceKey) {
    workspaceAssignmentError.value = 'Workspace muss ausgewählt sein.';
    return;
  }

  workspaceAssignmentMutatingPluginId.value = pluginId;
  try {
    const result = await request<PluginLifecycleApiResponse>(
      `/api/plugins/${encodeURIComponent(pluginId)}/${isEnabled ? 'activate' : 'deactivate'}`,
      {
        method: 'POST',
        body: {
          requestedBy: auth.session.value?.userId || null,
          workspaceKey: selectedWorkspaceKey
        }
      }
    );

    if (!result.isSuccess) {
      workspaceAssignmentError.value = result.message || 'Workspace-Zuweisung fehlgeschlagen.';
      return;
    }

    await loadWorkspaceAssignments();
    await loadPluginDiagnostics();
  } catch (error) {
    workspaceAssignmentError.value = extractErrorMessage(error, 'Workspace-Zuweisung fehlgeschlagen.');
  } finally {
    workspaceAssignmentMutatingPluginId.value = null;
  }
}

async function checkWorkspaceEntitlement(): Promise<void> {
  entitlementError.value = null;
  workspaceEntitlementResult.value = null;

  if (!entitlementState.pluginId || !entitlementState.workspaceKey) {
    entitlementError.value = 'Plugin und Workspace müssen ausgewählt sein.';
    return;
  }

  workspaceEntitlementLoading.value = true;

  const response = await requestSafe<PluginEntitlementStatus>(
    `/api/plugins/workspaces/${encodeURIComponent(entitlementState.workspaceKey)}/entitlements/${encodeURIComponent(entitlementState.pluginId)}`
  );

  if (response.ok && response.data) {
    workspaceEntitlementResult.value = response.data;
  } else {
    entitlementError.value = 'Workspace-Entitlement konnte nicht geladen werden.';
  }

  workspaceEntitlementLoading.value = false;
}

async function checkTenantEntitlement(): Promise<void> {
  entitlementError.value = null;
  tenantEntitlementResult.value = null;

  if (!entitlementState.pluginId || !entitlementState.tenantId) {
    entitlementError.value = 'Plugin und Tenant müssen ausgewählt sein.';
    return;
  }

  tenantEntitlementLoading.value = true;

  const response = await requestSafe<PluginEntitlementStatus>(
    `/api/plugins/tenants/${encodeURIComponent(entitlementState.tenantId)}/entitlements/${encodeURIComponent(entitlementState.pluginId)}`
  );

  if (response.ok && response.data) {
    tenantEntitlementResult.value = response.data;
  } else {
    entitlementError.value = 'Tenant-Entitlement konnte nicht geladen werden.';
  }

  tenantEntitlementLoading.value = false;
}

function openInstallConfirmModal(): void {
  installError.value = null;
  installInfo.value = null;
  confirmInstallOpen.value = true;
}

function openInstallModal(): void {
  confirmInstallOpen.value = false;
  installModalOpen.value = true;
}

function closeInstallModal(): void {
  installModalOpen.value = false;
}

function openUpdateNuGetModal(pluginId: string): void {
  updateNuGetError.value = null;
  updateNuGetState.pluginId = pluginId;
  updateNuGetState.packageId = pluginId;
  updateNuGetState.packageVersion = '';
  updateNuGetState.assemblyFileName = '';
  updateNuGetState.entryTypeName = '';
  updateNuGetModalOpen.value = true;
}

function closeUpdateNuGetModal(): void {
  updateNuGetModalOpen.value = false;
}

function resetInstallState(): void {
  installSource.value = 'local';
  installError.value = null;
  installInfo.value = null;
  selectedZipFile.value = null;
  localState.pluginId = '';
  localState.buildIfNeeded = true;
  localState.forceBuild = false;
  nugetState.packageId = '';
  nugetState.packageVersion = '';
  nugetState.assemblyFileName = '';
  nugetState.entryTypeName = '';
  assemblyState.assemblyPath = '';
  assemblyState.entryTypeName = '';
}

function onZipFileChanged(event: Event): void {
  const target = event.target as HTMLInputElement | null;
  selectedZipFile.value = target?.files?.[0] ?? null;
}

async function submitInstall(): Promise<void> {
  installError.value = null;
  installInfo.value = null;

  if (installSource.value === 'local') {
    const pluginId = localState.pluginId.trim();
    if (!pluginId) {
      installError.value = 'Plugin ID ist erforderlich.';
      return;
    }

    installPending.value = true;

    const payload: InstallLocalPluginRequest = {
      pluginId,
      buildIfNeeded: localState.buildIfNeeded,
      forceBuild: localState.forceBuild,
      requestedBy: auth.session.value?.userId || null
    };

    try {
      const result = await request<PluginLifecycleApiResponse>('/api/plugins/install/local', {
        method: 'POST',
        body: payload
      });

      if (!result.isSuccess) {
        installError.value = result.message || 'Lokale Installation fehlgeschlagen.';
        return;
      }

      installModalOpen.value = false;
      resetInstallState();
      await reloadAllData();
    } catch (error) {
      installError.value = extractErrorMessage(error, 'Lokale Installation fehlgeschlagen.');
    } finally {
      installPending.value = false;
    }

    return;
  }

  if (installSource.value === 'assembly') {
    const assemblyPath = assemblyState.assemblyPath.trim();
    if (!assemblyPath) {
      installError.value = 'Assembly-Pfad ist erforderlich.';
      return;
    }

    installPending.value = true;

    try {
      const result = await request<PluginLifecycleApiResponse>('/api/plugins/install', {
        method: 'POST',
        body: {
          assemblyPath,
          entryTypeName: assemblyState.entryTypeName.trim() || null,
          requestedBy: auth.session.value?.userId || null
        }
      });

      if (!result.isSuccess) {
        installError.value = result.message || 'Assembly-Installation fehlgeschlagen.';
        return;
      }

      installModalOpen.value = false;
      resetInstallState();
      await reloadAllData();
    } catch (error) {
      installError.value = extractErrorMessage(error, 'Assembly-Installation fehlgeschlagen.');
    } finally {
      installPending.value = false;
    }

    return;
  }

  if (installSource.value === 'zip') {
    if (!selectedZipFile.value) {
      installError.value = 'Bitte eine ZIP-Datei auswählen.';
      return;
    }

    installInfo.value = `ZIP-Upload vorbereitet (${selectedZipFile.value.name}). Die eigentliche Installation wird im nächsten Schritt implementiert.`;
    return;
  }

  const packageId = nugetState.packageId.trim();
  const packageVersion = nugetState.packageVersion.trim();

  if (!packageId || !packageVersion) {
    installError.value = 'Package ID und Version sind erforderlich.';
    return;
  }

  installPending.value = true;

  const payload: InstallNuGetPluginRequest = {
    packageId,
    packageVersion,
    assemblyFileName: nugetState.assemblyFileName.trim() || null,
    entryTypeName: nugetState.entryTypeName.trim() || null,
    requestedBy: auth.session.value?.userId || null
  };

  try {
    const result = await request<PluginLifecycleApiResponse>('/api/plugins/install/nuget', {
      method: 'POST',
      body: payload
    });

    if (!result.isSuccess) {
      installError.value = result.message || 'NuGet-Installation fehlgeschlagen.';
      return;
    }

    installModalOpen.value = false;
    resetInstallState();
    await reloadAllData();
  } catch (error) {
    installError.value = extractErrorMessage(error, 'NuGet-Installation fehlgeschlagen.');
  } finally {
    installPending.value = false;
  }
}

async function submitNuGetUpdate(): Promise<void> {
  updateNuGetError.value = null;

  const pluginId = updateNuGetState.pluginId.trim();
  const packageId = updateNuGetState.packageId.trim();
  const packageVersion = updateNuGetState.packageVersion.trim();

  if (!pluginId || !packageId || !packageVersion) {
    updateNuGetError.value = 'Plugin ID, Package ID und Version sind erforderlich.';
    return;
  }

  updateNuGetPending.value = true;

  try {
    const result = await request<PluginLifecycleApiResponse>(`/api/plugins/${encodeURIComponent(pluginId)}/update/nuget`, {
      method: 'POST',
      body: {
        packageId,
        packageVersion,
        assemblyFileName: updateNuGetState.assemblyFileName.trim() || null,
        entryTypeName: updateNuGetState.entryTypeName.trim() || null,
        requestedBy: auth.session.value?.userId || null
      }
    });

    if (!result.isSuccess) {
      updateNuGetError.value = result.message || 'NuGet-Update fehlgeschlagen.';
      return;
    }

    updateNuGetModalOpen.value = false;
    await reloadAllData();
  } catch (error) {
    updateNuGetError.value = extractErrorMessage(error, 'NuGet-Update fehlgeschlagen.');
  } finally {
    updateNuGetPending.value = false;
  }
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

const auditColumns: TableColumn<PluginAuditEntry>[] = [{
  accessorKey: 'occurredAtUtc',
  header: 'Zeit',
  cell: ({ row }) => toLocalDateTime(row.original.occurredAtUtc)
}, {
  accessorKey: 'action',
  header: 'Aktion'
}, {
  accessorKey: 'pluginId',
  header: 'Plugin'
}, {
  accessorKey: 'isSuccess',
  header: 'Ergebnis',
  cell: ({ row }) => h(UBadge, {
    variant: 'subtle',
    color: row.original.isSuccess ? 'success' : 'error'
  }, () => row.original.isSuccess ? 'ok' : 'failed')
}, {
  accessorKey: 'requestedBy',
  header: 'User'
}];

const supportColumns: TableColumn<PluginContractSupport>[] = [{
  accessorKey: 'contractVersion',
  header: 'Contract'
}, {
  accessorKey: 'supportStatus',
  header: 'Status'
}, {
  accessorKey: 'isInstallable',
  header: 'Installable',
  cell: ({ row }) => h(UBadge, {
    variant: 'subtle',
    color: row.original.isInstallable ? 'success' : 'error'
  }, () => row.original.isInstallable ? 'yes' : 'no')
}, {
  accessorKey: 'emitsWarning',
  header: 'Warning',
  cell: ({ row }) => h(UBadge, {
    variant: 'subtle',
    color: row.original.emitsWarning ? 'warning' : 'neutral'
  }, () => row.original.emitsWarning ? 'yes' : 'no')
}, {
  accessorKey: 'message',
  header: 'Message'
}];

const compatibilityColumns: TableColumn<PluginContractCompatibility>[] = [{
  accessorKey: 'contractVersion',
  header: 'Contract'
}, {
  accessorKey: 'result',
  header: 'Result'
}, {
  accessorKey: 'isCompatible',
  header: 'Compatible',
  cell: ({ row }) => h(UBadge, {
    variant: 'subtle',
    color: row.original.isCompatible ? 'success' : 'error'
  }, () => row.original.isCompatible ? 'yes' : 'no')
}, {
  accessorKey: 'hostVersion',
  header: 'Host'
}, {
  accessorKey: 'coreVersion',
  header: 'Core'
}];

const trustedSignerColumns: TableColumn<TrustedPluginSigner>[] = [{
  accessorKey: 'displayName',
  header: 'Signer'
}, {
  accessorKey: 'publisherId',
  header: 'Publisher'
}, {
  accessorKey: 'thumbprint',
  header: 'Thumbprint'
}, {
  accessorKey: 'source',
  header: 'Source'
}];

watch(installModalOpen, (isOpen) => {
  if (!isOpen) {
    resetInstallState();
  }
});

watch(updateNuGetModalOpen, (isOpen) => {
  if (!isOpen) {
    updateNuGetError.value = null;
  }
});

watch(
  () => workspaceAssignmentWorkspaceKey.value,
  () => {
    void loadWorkspaceAssignments();
  }
);

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

        <UPageCard title="Workspace-Zuweisung">
          <div class="space-y-4">
            <div class="grid grid-cols-1 md:grid-cols-[280px_1fr] gap-4">
              <UFormField label="Workspace" required>
                <USelect
                  v-model="workspaceAssignmentWorkspaceKey"
                  :items="workspaceOptions"
                  class="w-full"
                />
              </UFormField>

              <UAlert
                color="neutral"
                variant="subtle"
                icon="i-lucide-info"
                title="Plugin-Aktivierung pro Workspace"
                description="Diese Zuordnung steuert, ob ein installiertes Plugin im ausgewählten Workspace aktiv ist."
              />
            </div>

            <UAlert
              v-if="workspaceAssignmentError"
              color="error"
              variant="subtle"
              icon="i-lucide-triangle-alert"
              :title="workspaceAssignmentError"
            />

            <UAlert
              v-if="workspaceAssignmentMutatingPluginId"
              color="neutral"
              variant="subtle"
              icon="i-lucide-loader-circle"
              :title="`Workspace-Zuweisung für ${workspaceAssignmentMutatingPluginId} wird aktualisiert...`"
            />

            <UEmpty
              v-if="workspaceOptions.length === 0"
              icon="i-lucide-store"
              title="Keine Workspaces vorhanden"
              description="Lege zuerst einen Workspace an, um Plugin-Zuweisungen zu konfigurieren."
            />

            <UEmpty
              v-else-if="workspaceAssignmentRows.length === 0"
              icon="i-lucide-plug-zap"
              title="Keine Plugins installiert"
              description="Installiere zuerst ein Plugin, um es einem Workspace zuzuweisen."
            />

            <div v-else class="grid grid-cols-1 xl:grid-cols-2 gap-4">
              <UCard v-for="item in workspaceAssignmentRows" :key="item.pluginId">
                <template #header>
                  <div class="flex items-center justify-between gap-2">
                    <div>
                      <p class="font-semibold">{{ item.displayName }}</p>
                      <p class="text-xs text-muted">{{ item.pluginId }}</p>
                    </div>
                    <UBadge
                      :color="item.isEntitled ? 'success' : 'warning'"
                      variant="subtle"
                    >
                      {{ item.isEntitled ? 'assigned' : 'not assigned' }}
                    </UBadge>
                  </div>
                </template>

                <div class="flex items-center justify-between gap-3">
                  <UBadge :color="toStateColor(item.state)" variant="subtle" class="capitalize">
                    {{ toStateLabel(item.state) }}
                  </UBadge>

                  <div class="flex gap-2">
                    <UButton
                      color="success"
                      variant="soft"
                      icon="i-lucide-power"
                      :loading="workspaceAssignmentLoading || workspaceAssignmentMutatingPluginId === item.pluginId"
                      :disabled="item.state === 3"
                      @click="setWorkspacePluginAssignment(item.pluginId, true)"
                    >
                      Aktivieren
                    </UButton>
                    <UButton
                      color="warning"
                      variant="soft"
                      icon="i-lucide-circle-off"
                      :loading="workspaceAssignmentLoading || workspaceAssignmentMutatingPluginId === item.pluginId"
                      :disabled="item.state === 3"
                      @click="setWorkspacePluginAssignment(item.pluginId, false)"
                    >
                      Deaktivieren
                    </UButton>
                  </div>
                </div>
              </UCard>
            </div>
          </div>
        </UPageCard>

        <div class="grid grid-cols-1 xl:grid-cols-2 gap-4">
          <UPageCard title="Contract Support">
            <UTable :data="contractSupport" :columns="supportColumns" :loading="diagnosticsLoading" />
          </UPageCard>

          <UPageCard title="Contract Compatibility">
            <UTable :data="contractCompatibility" :columns="compatibilityColumns" :loading="diagnosticsLoading" />
          </UPageCard>
        </div>

        <div class="grid grid-cols-1 xl:grid-cols-2 gap-4">
          <UPageCard title="Trusted Signers">
            <UTable :data="trustedSigners" :columns="trustedSignerColumns" :loading="diagnosticsLoading" />
          </UPageCard>

          <UPageCard title="Audit Log (letzte 100)">
            <UTable :data="pluginAuditEntries" :columns="auditColumns" :loading="diagnosticsLoading" />
          </UPageCard>
        </div>

        <UPageCard title="Entitlements prüfen">
          <div class="grid grid-cols-1 xl:grid-cols-2 gap-4">
            <UCard>
              <template #header>
                Workspace Entitlement
              </template>

              <div class="space-y-3">
                <UFormField label="Plugin" required>
                  <USelect v-model="entitlementState.pluginId" :items="pluginOptions" class="w-full" />
                </UFormField>
                <UFormField label="Workspace" required>
                  <USelect v-model="entitlementState.workspaceKey" :items="workspaceOptions" class="w-full" />
                </UFormField>
                <UButton
                  color="primary"
                  icon="i-lucide-search"
                  :loading="workspaceEntitlementLoading"
                  @click="checkWorkspaceEntitlement"
                >
                  Prüfen
                </UButton>

                <UAlert
                  v-if="workspaceEntitlementResult"
                  :color="workspaceEntitlementResult.isEntitled ? 'success' : 'warning'"
                  variant="subtle"
                  icon="i-lucide-shield-check"
                  :title="workspaceEntitlementResult.isEntitled ? 'Entitled' : 'Nicht entitled'"
                  :description="`Plugin ${workspaceEntitlementResult.pluginId} für Workspace ${workspaceEntitlementResult.workspaceKey}`"
                />
              </div>
            </UCard>

            <UCard>
              <template #header>
                Tenant Entitlement (Legacy)
              </template>

              <div class="space-y-3">
                <UFormField label="Plugin" required>
                  <USelect v-model="entitlementState.pluginId" :items="pluginOptions" class="w-full" />
                </UFormField>
                <UFormField label="Tenant" required>
                  <USelect v-model="entitlementState.tenantId" :items="tenantOptions" class="w-full" />
                </UFormField>
                <UButton
                  color="primary"
                  icon="i-lucide-search"
                  :loading="tenantEntitlementLoading"
                  @click="checkTenantEntitlement"
                >
                  Prüfen
                </UButton>

                <UAlert
                  v-if="tenantEntitlementResult"
                  :color="tenantEntitlementResult.isEntitled ? 'success' : 'warning'"
                  variant="subtle"
                  icon="i-lucide-shield-check"
                  :title="tenantEntitlementResult.isEntitled ? 'Entitled' : 'Nicht entitled'"
                  :description="`Plugin ${tenantEntitlementResult.pluginId} für Tenant ${tenantEntitlementResult.workspaceKey}`"
                />
              </div>
            </UCard>
          </div>

          <UAlert
            v-if="entitlementError"
            color="error"
            variant="subtle"
            icon="i-lucide-triangle-alert"
            :title="entitlementError"
            class="mt-4"
          />
        </UPageCard>
      </div>
    </template>
  </UDashboardPanel>

  <UModal
    v-model:open="confirmInstallOpen"
    title="Warnung"
    :ui="{ footer: 'justify-end gap-2' }"
  >
    <template #body>
      <p class="text-sm text-muted">
        Erweiterungen, die nicht aus dem Callora-Store stammen, werden nicht automatisch verifiziert. Nur vertrauenswürdige Quellen verwenden.
      </p>
    </template>
    <template #footer>
      <UButton color="neutral" variant="ghost" @click="confirmInstallOpen = false">
        Abbrechen
      </UButton>
      <UButton color="primary" @click="openInstallModal">
        Bestätigen
      </UButton>
    </template>
  </UModal>

  <UModal
    v-model:open="installModalOpen"
    title="Plugin installieren"
    :ui="{ footer: 'justify-end gap-2' }"
  >
    <template #body>
      <div class="space-y-4">
        <UAlert
          v-if="installError"
          color="error"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          :title="installError"
        />
        <UAlert
          v-if="installInfo"
          color="info"
          variant="subtle"
          icon="i-lucide-info"
          :title="installInfo"
        />

        <UFormField label="Quelle">
          <USelect
            v-model="installSource"
            class="w-full"
            :items="installSourceOptions"
          />
        </UFormField>

        <div v-if="installSource === 'local'" class="space-y-3">
          <UFormField label="Plugin ID" required>
            <UInput v-model="localState.pluginId" class="w-full" placeholder="template-alpha" />
          </UFormField>
          <UFormField label="Kompilierung">
            <UCheckbox
              v-model="localState.buildIfNeeded"
              label="Automatisch kompilieren, wenn keine DLL vorhanden ist"
            />
          </UFormField>
          <UFormField label="Rebuild">
            <UCheckbox
              v-model="localState.forceBuild"
              label="Neu kompilieren erzwingen (no-incremental)"
            />
          </UFormField>
        </div>

        <div v-else-if="installSource === 'nuget'" class="space-y-3">
          <UFormField label="Package ID" required>
            <UInput v-model="nugetState.packageId" class="w-full" placeholder="Callora.Plugin.Example" />
          </UFormField>
          <UFormField label="Version" required>
            <UInput v-model="nugetState.packageVersion" class="w-full" placeholder="1.0.0" />
          </UFormField>
          <UFormField label="Assembly-Dateiname (optional)">
            <UInput v-model="nugetState.assemblyFileName" class="w-full" placeholder="Callora.Plugin.Example.dll" />
          </UFormField>
          <UFormField label="Entry Type (optional)">
            <UInput v-model="nugetState.entryTypeName" class="w-full" placeholder="Example.Plugin.EntryPoint" />
          </UFormField>
        </div>

        <div v-else-if="installSource === 'assembly'" class="space-y-3">
          <UFormField label="Assembly Pfad" required>
            <UInput v-model="assemblyState.assemblyPath" class="w-full" placeholder="/app/custom/plugins/MyPlugin/bin/Release/net8.0/MyPlugin.dll" />
          </UFormField>
          <UFormField label="Entry Type (optional)">
            <UInput v-model="assemblyState.entryTypeName" class="w-full" placeholder="Example.Plugin.EntryPoint" />
          </UFormField>
        </div>

        <div v-else class="space-y-3">
          <UFormField label="ZIP-Datei" required>
            <UInput
              type="file"
              accept=".zip,application/zip,application/x-zip-compressed"
              class="w-full"
              @change="onZipFileChanged"
            />
          </UFormField>
          <UFormField label="Ausgewählte Datei">
            <UInput :model-value="selectedZipFileName" disabled />
          </UFormField>
          <UAlert
            color="warning"
            variant="subtle"
            icon="i-lucide-construction"
            title="ZIP-Installation wird im nächsten Schritt serverseitig aktiviert."
          />
        </div>
      </div>
    </template>
    <template #footer>
      <UButton color="neutral" variant="ghost" @click="closeInstallModal">
        Schließen
      </UButton>
      <UButton color="primary" :loading="installPending" @click="submitInstall">
        {{ installSource === 'zip' ? 'Vorbereiten' : 'Installieren' }}
      </UButton>
    </template>
  </UModal>

  <UModal
    v-model:open="updateNuGetModalOpen"
    title="Plugin per NuGet aktualisieren"
    :ui="{ footer: 'justify-end gap-2' }"
  >
    <template #body>
      <div class="space-y-4">
        <UAlert
          v-if="updateNuGetError"
          color="error"
          variant="subtle"
          icon="i-lucide-triangle-alert"
          :title="updateNuGetError"
        />

        <UFormField label="Plugin ID" required>
          <UInput v-model="updateNuGetState.pluginId" class="w-full" disabled />
        </UFormField>
        <UFormField label="Package ID" required>
          <UInput v-model="updateNuGetState.packageId" class="w-full" />
        </UFormField>
        <UFormField label="Version" required>
          <UInput v-model="updateNuGetState.packageVersion" class="w-full" placeholder="1.0.1" />
        </UFormField>
        <UFormField label="Assembly-Dateiname (optional)">
          <UInput v-model="updateNuGetState.assemblyFileName" class="w-full" placeholder="Callora.Plugin.Example.dll" />
        </UFormField>
        <UFormField label="Entry Type (optional)">
          <UInput v-model="updateNuGetState.entryTypeName" class="w-full" placeholder="Example.Plugin.EntryPoint" />
        </UFormField>
      </div>
    </template>
    <template #footer>
      <UButton color="neutral" variant="ghost" @click="closeUpdateNuGetModal">
        Abbrechen
      </UButton>
      <UButton color="primary" :loading="updateNuGetPending" @click="submitNuGetUpdate">
        Aktualisieren
      </UButton>
    </template>
  </UModal>
</template>
