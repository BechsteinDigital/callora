<script setup lang="ts">
import { h, resolveComponent } from 'vue';
import * as z from 'zod';
import type { FormSubmitEvent, TableColumn } from '@nuxt/ui';
import type { AdminWorkspace, UpsertAdminWorkspaceRequest } from '~/types/admin-workspaces';
import type {
  AdminThemeDefinition,
  AdminWorkspaceThemeSettingField,
  AdminWorkspaceThemeSettings,
  AdminWorkspaceThemeAssignment,
  AdminWorkspaceThemeEffective,
  AssignWorkspaceThemeRequest,
  UpsertWorkspaceThemeSettingsRequest
} from '~/types/admin-workspace-theme';

const route = useRoute();
const auth = useAdminAuth();
const { request, requestSafe } = useAdminApi();

const workspaceKey = computed(() => String(route.params.workspaceKey || '').trim());

const loading = ref(true);
const savingGeneral = ref(false);
const assigningTheme = ref(false);
const clearingTheme = ref(false);
const loadingThemes = ref(false);
const savingThemeSettings = ref(false);

const generalError = ref<string | null>(null);
const themeError = ref<string | null>(null);
const generalSuccess = ref<string | null>(null);
const themeSuccess = ref<string | null>(null);

const activeTab = ref<'general' | 'theme' | 'analysis'>('general');
const themePickerOpen = ref(false);
const themeSearch = ref('');
const selectedTheme = ref<string>('');

const workspace = ref<AdminWorkspace | null>(null);
const assignment = ref<AdminWorkspaceThemeAssignment | null>(null);
const definitions = ref<AdminThemeDefinition[]>([]);
const effectiveTemplates = ref<AdminWorkspaceThemeEffective[]>([]);
const themeSettings = ref<AdminWorkspaceThemeSettings | null>(null);
const themeSettingsState = reactive<Record<string, unknown>>({});

const generalSchema = z.object({
  workspaceKey: z.string().min(1),
  displayName: z.string().min(1, 'Display name is required'),
  workspaceType: z.string().min(1, 'Type is required'),
  isActive: z.boolean(),
  publicBaseUrl: z.string().optional()
});

type GeneralSchema = z.output<typeof generalSchema>;

const generalState = reactive<GeneralSchema>({
  workspaceKey: '',
  displayName: '',
  workspaceType: 'voice',
  isActive: true,
  publicBaseUrl: ''
});

const themeCandidates = computed(() => {
  const seen = new Set<string>();
  const rows: Array<{ key: string; pluginId: string; version: string; definitions: number; displayName: string }> = [];

  for (const definition of definitions.value) {
    const key = `${definition.pluginId}@${definition.version}`;
    if (seen.has(key)) {
      const existing = rows.find((entry) => entry.key === key);
      if (existing) {
        existing.definitions += 1;
      }
      continue;
    }

    seen.add(key);
    rows.push({
      key,
      pluginId: definition.pluginId,
      version: definition.version,
      definitions: 1,
      displayName: definition.displayName
    });
  }

  const value = themeSearch.value.trim().toLowerCase();
  if (!value) {
    return rows;
  }

  return rows.filter((row) => {
    return row.displayName.toLowerCase().includes(value) ||
      row.pluginId.toLowerCase().includes(value) ||
      row.version.toLowerCase().includes(value);
  });
});

const tabs = [{
  key: 'general',
  label: 'General'
}, {
  key: 'theme',
  label: 'Theme'
}, {
  key: 'analysis',
  label: 'Analysis'
}] as const;

const groupedThemeFields = computed(() => {
  const groups = new Map<string, AdminWorkspaceThemeSettingField[]>();
  const fields = [...(themeSettings.value?.fields ?? [])]
    .filter((field) => field.isActive)
    .sort((a, b) => a.sortOrder - b.sortOrder || a.label.localeCompare(b.label));

  for (const field of fields) {
    const groupName = field.groupName?.trim() || 'General';
    const existing = groups.get(groupName);
    if (existing) {
      existing.push(field);
      continue;
    }

    groups.set(groupName, [field]);
  }

  return Array.from(groups.entries()).map(([groupName, fieldsInGroup]) => ({
    groupName,
    fields: fieldsInGroup
  }));
});

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

function resetMessages(): void {
  generalError.value = null;
  themeError.value = null;
  generalSuccess.value = null;
  themeSuccess.value = null;
}

function toLocalDateTime(value: string | null): string {
  if (!value) {
    return '-';
  }

  return new Date(value).toLocaleString();
}

async function loadGeneralData(): Promise<void> {
  const workspaceResult = await requestSafe<AdminWorkspace>(`/api/workspaces/${encodeURIComponent(workspaceKey.value)}`);

  if (!workspaceResult.ok || !workspaceResult.data) {
    throw new Error('Workspace could not be loaded.');
  }

  workspace.value = workspaceResult.data;

  generalState.workspaceKey = workspaceResult.data.workspaceKey;
  generalState.displayName = workspaceResult.data.displayName;
  generalState.workspaceType = workspaceResult.data.workspaceType;
  generalState.isActive = workspaceResult.data.isActive;
  generalState.publicBaseUrl = workspaceResult.data.publicBaseUrl || '';
}

async function loadThemeData(): Promise<void> {
  loadingThemes.value = true;

  const [assignmentResult, definitionsResult, effectiveResult, settingsResult] = await Promise.all([
    requestSafe<AdminWorkspaceThemeAssignment>(`/api/themes/workspaces/${encodeURIComponent(workspaceKey.value)}`),
    requestSafe<AdminThemeDefinition[]>('/api/themes/definitions?surface=workspace&active=true'),
    requestSafe<AdminWorkspaceThemeEffective[]>(`/api/themes/workspaces/${encodeURIComponent(workspaceKey.value)}/effective`),
    requestSafe<AdminWorkspaceThemeSettings>(`/api/themes/workspaces/${encodeURIComponent(workspaceKey.value)}/settings`)
  ]);

  assignment.value = assignmentResult.ok ? assignmentResult.data : null;
  definitions.value = definitionsResult.ok ? (definitionsResult.data ?? []) : [];
  effectiveTemplates.value = effectiveResult.ok ? (effectiveResult.data ?? []) : [];
  themeSettings.value = settingsResult.ok ? (settingsResult.data ?? null) : null;
  hydrateThemeSettingsState();

  if (assignment.value?.themePluginId && assignment.value.themeVersion) {
    selectedTheme.value = `${assignment.value.themePluginId}@${assignment.value.themeVersion}`;
  } else if (!selectedTheme.value) {
    selectedTheme.value = themeCandidates.value[0]?.key ?? '';
  }

  loadingThemes.value = false;
}

function parseJsonOrNull(raw: string | null | undefined): unknown {
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw);
  } catch {
    return raw;
  }
}

function parseOptions(field: AdminWorkspaceThemeSettingField): Array<{ label: string; value: string }> {
  const parsed = parseJsonOrNull(field.optionsJson);
  if (!parsed) {
    return [];
  }

  if (Array.isArray(parsed)) {
    return parsed
      .map((item) => {
        if (typeof item === 'string') {
          return { label: item, value: item };
        }

        if (item && typeof item === 'object') {
          const value = String((item as Record<string, unknown>).value ?? (item as Record<string, unknown>).id ?? '');
          const label = String((item as Record<string, unknown>).label ?? value);
          if (!value) {
            return null;
          }

          return { label, value };
        }

        return null;
      })
      .filter((x): x is { label: string; value: string } => Boolean(x));
  }

  if (parsed && typeof parsed === 'object') {
    return Object.entries(parsed as Record<string, unknown>)
      .map(([value, label]) => ({
        value,
        label: typeof label === 'string' ? label : value
      }));
  }

  return [];
}

function getFieldValue(field: AdminWorkspaceThemeSettingField): unknown {
  if (!themeSettings.value) {
    return null;
  }

  return themeSettingsState[field.settingKey] ?? parseJsonOrNull(field.defaultValueJson);
}

function setFieldValue(field: AdminWorkspaceThemeSettingField, value: unknown): void {
  themeSettingsState[field.settingKey] = value;
}

function hydrateThemeSettingsState(): void {
  for (const key of Object.keys(themeSettingsState)) {
    delete themeSettingsState[key];
  }

  if (!themeSettings.value) {
    return;
  }

  for (const field of themeSettings.value.fields) {
    const rawValue = themeSettings.value.valuesByKey[field.settingKey];
    if (rawValue !== undefined) {
      themeSettingsState[field.settingKey] = parseJsonOrNull(rawValue);
      continue;
    }

    themeSettingsState[field.settingKey] = parseJsonOrNull(field.defaultValueJson);
  }
}

async function saveThemeSettings(): Promise<void> {
  if (!workspace.value || !themeSettings.value || !themeSettings.value.hasAssignedTheme) {
    return;
  }

  savingThemeSettings.value = true;
  themeError.value = null;
  themeSuccess.value = null;

  const valuesByKey: Record<string, unknown> = {};
  for (const field of themeSettings.value.fields) {
    valuesByKey[field.settingKey] = themeSettingsState[field.settingKey] ?? null;
  }

  const payload: UpsertWorkspaceThemeSettingsRequest = { valuesByKey };

  try {
    const response = await request<AdminWorkspaceThemeSettings>(
      `/api/themes/workspaces/${encodeURIComponent(workspace.value.workspaceKey)}/settings`,
      {
        method: 'PUT',
        body: payload
      }
    );

    themeSettings.value = response;
    hydrateThemeSettingsState();
    themeSuccess.value = 'Theme settings saved.';
  } catch (error) {
    themeError.value = extractErrorMessage(error, 'Theme settings could not be saved.');
  } finally {
    savingThemeSettings.value = false;
  }
}

async function loadAll(): Promise<void> {
  loading.value = true;
  resetMessages();

  try {
    await loadGeneralData();
    await loadThemeData();
  } catch (error) {
    generalError.value = extractErrorMessage(error, 'Workspace could not be loaded.');
  } finally {
    loading.value = false;
  }
}

async function saveGeneral(event: FormSubmitEvent<GeneralSchema>): Promise<void> {
  if (!workspace.value) {
    return;
  }

  savingGeneral.value = true;
  generalError.value = null;
  generalSuccess.value = null;

  const payload: UpsertAdminWorkspaceRequest = {
    displayName: event.data.displayName.trim(),
    workspaceType: event.data.workspaceType.trim(),
    isActive: event.data.isActive,
    publicBaseUrl: event.data.publicBaseUrl?.trim() || null
  };

  try {
    const response = await request<AdminWorkspace>(`/api/workspaces/${encodeURIComponent(workspace.value.workspaceKey)}`, {
      method: 'PUT',
      body: payload
    });

    workspace.value = response;
    generalState.workspaceKey = response.workspaceKey;
    generalState.displayName = response.displayName;
    generalState.workspaceType = response.workspaceType;
    generalState.isActive = response.isActive;
    generalState.publicBaseUrl = response.publicBaseUrl || '';
    generalSuccess.value = 'Workspace settings saved.';
  } catch (error) {
    generalError.value = extractErrorMessage(error, 'Workspace could not be saved.');
  } finally {
    savingGeneral.value = false;
  }
}

async function assignTheme(): Promise<void> {
  if (!workspace.value || !selectedTheme.value) {
    return;
  }

  assigningTheme.value = true;
  themeError.value = null;
  themeSuccess.value = null;

  const [themePluginIdRaw, themeVersionRaw] = selectedTheme.value.split('@');
  const themePluginId = themePluginIdRaw?.trim() ?? '';
  const themeVersion = themeVersionRaw?.trim() ?? '';
  if (!themePluginId || !themeVersion) {
    themeError.value = 'Selected theme is invalid.';
    assigningTheme.value = false;
    return;
  }
  const payload: AssignWorkspaceThemeRequest = {
    themePluginId,
    themeVersion,
    assignedBy: auth.session.value?.userId ?? null
  };

  try {
    const response = await request<AdminWorkspaceThemeAssignment>(
      `/api/themes/workspaces/${encodeURIComponent(workspace.value.workspaceKey)}`,
      {
        method: 'PUT',
        body: payload
      }
    );

    assignment.value = response;
    themePickerOpen.value = false;
    themeSuccess.value = `Theme ${themePluginId}@${themeVersion} assigned.`;
    await loadThemeData();
  } catch (error) {
    themeError.value = extractErrorMessage(error, 'Theme could not be assigned.');
  } finally {
    assigningTheme.value = false;
  }
}

async function clearTheme(): Promise<void> {
  if (!workspace.value) {
    return;
  }

  clearingTheme.value = true;
  themeError.value = null;
  themeSuccess.value = null;

  try {
    await request<void>(`/api/themes/workspaces/${encodeURIComponent(workspace.value.workspaceKey)}`, {
      method: 'DELETE'
    });

    assignment.value = null;
    themeSuccess.value = 'Theme assignment removed.';
    await loadThemeData();
  } catch (error) {
    themeError.value = extractErrorMessage(error, 'Theme assignment could not be removed.');
  } finally {
    clearingTheme.value = false;
  }
}

const UBadge = resolveComponent('UBadge');

const effectiveColumns: TableColumn<AdminWorkspaceThemeEffective>[] = [{
  accessorKey: 'templateKey',
  header: 'Template'
}, {
  accessorKey: 'displayName',
  header: 'Display Name'
}, {
  accessorKey: 'pluginId',
  header: 'Plugin'
}, {
  accessorKey: 'version',
  header: 'Version'
}, {
  accessorKey: 'source',
  header: 'Source',
  cell: ({ row }) => h(UBadge, { color: 'neutral', variant: 'subtle', class: 'capitalize' }, () => row.original.source)
}];

await loadAll();
</script>

<template>
  <UDashboardPanel id="workspace-detail">
    <template #header>
      <UDashboardNavbar :title="workspace?.displayName || workspaceKey">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>

        <template #right>
          <UButton
            color="neutral"
            variant="outline"
            icon="i-lucide-arrow-left"
            :to="'/workspaces'"
          >
            Back to Workspaces
          </UButton>
        </template>
      </UDashboardNavbar>

      <UDashboardToolbar>
        <template #left>
          <UButtonGroup size="sm">
            <UButton
              v-for="tab in tabs"
              :key="tab.key"
              :color="activeTab === tab.key ? 'primary' : 'neutral'"
              :variant="activeTab === tab.key ? 'solid' : 'ghost'"
              @click="activeTab = tab.key"
            >
              {{ tab.label }}
            </UButton>
          </UButtonGroup>
        </template>

        <template #right>
          <UButton
            color="neutral"
            variant="ghost"
            icon="i-lucide-refresh-cw"
            :loading="loading"
            @click="loadAll"
          />
        </template>
      </UDashboardToolbar>
    </template>

    <template #body>
      <UAlert
        v-if="generalError"
        color="error"
        variant="soft"
        :description="generalError"
        icon="i-lucide-triangle-alert"
      />

      <template v-if="activeTab === 'general'">
        <UCard>
          <template #header>
            <div>
              <p class="text-xs text-muted uppercase">Workspace</p>
              <p class="text-xl font-semibold text-highlighted">General Settings</p>
            </div>
          </template>

          <UAlert
            v-if="generalSuccess"
            class="mb-4"
            color="success"
            variant="soft"
            :description="generalSuccess"
            icon="i-lucide-check-circle"
          />

          <UForm
            :schema="generalSchema"
            :state="generalState"
            class="grid gap-4 md:grid-cols-2"
            @submit="saveGeneral"
          >
            <UFormField label="Workspace Key" name="workspaceKey">
              <UInput v-model="generalState.workspaceKey" disabled />
            </UFormField>

            <UFormField label="Display Name" name="displayName">
              <UInput v-model="generalState.displayName" />
            </UFormField>

            <UFormField label="Type" name="workspaceType">
              <UInput v-model="generalState.workspaceType" />
            </UFormField>

            <UFormField
              label="Public URL"
              name="publicBaseUrl"
              description="Examples: dialer.example.de or localhost/dialer"
            >
              <UInput v-model="generalState.publicBaseUrl" />
            </UFormField>

            <UFormField label="Active" name="isActive">
              <USwitch v-model="generalState.isActive" />
            </UFormField>

            <UFormField label="Resolved Route">
              <UInput :model-value="workspace ? `${workspace.publicHost || '*'}${workspace.publicPathPrefix}` : '-'" disabled />
            </UFormField>

            <UFormField label="Updated">
              <UInput :model-value="workspace ? toLocalDateTime(workspace.updatedAtUtc) : '-'" disabled />
            </UFormField>

            <div class="md:col-span-2 flex justify-end">
              <UButton type="submit" :loading="savingGeneral">Save General Settings</UButton>
            </div>
          </UForm>
        </UCard>
      </template>

      <template v-else-if="activeTab === 'theme'">
        <div class="space-y-4">
          <UCard>
            <template #header>
              <div>
                <p class="text-xs text-muted uppercase">Theme Assignment</p>
                <p class="text-xl font-semibold text-highlighted">Workspace Theme</p>
              </div>
            </template>

            <UAlert
              v-if="themeError"
              class="mb-4"
              color="error"
              variant="soft"
              :description="themeError"
              icon="i-lucide-triangle-alert"
            />

            <UAlert
              v-if="themeSuccess"
              class="mb-4"
              color="success"
              variant="soft"
              :description="themeSuccess"
              icon="i-lucide-check-circle"
            />

            <div class="flex flex-wrap items-center justify-between gap-3">
              <div class="space-y-1">
                <p class="text-sm font-medium text-highlighted">
                  {{ assignment?.themePluginId ? `${assignment.themePluginId}@${assignment.themeVersion}` : 'No theme assigned' }}
                </p>
                <p class="text-xs text-muted">
                  Assigned by: {{ assignment?.assignedBy || '-' }} · {{ assignment?.assignedAtUtc ? toLocalDateTime(assignment.assignedAtUtc) : '-' }}
                </p>
              </div>

              <div class="flex items-center gap-2">
                <UButton
                  color="neutral"
                  variant="outline"
                  icon="i-lucide-palette"
                  @click="themePickerOpen = true"
                >
                  Change Theme
                </UButton>

                <UButton
                  color="error"
                  variant="outline"
                  icon="i-lucide-eraser"
                  :loading="clearingTheme"
                  @click="clearTheme"
                >
                  Remove
                </UButton>
              </div>
            </div>
          </UCard>

          <UCard>
            <template #header>
              <div>
                <p class="text-xs text-muted uppercase">Theme Settings</p>
                <p class="text-xl font-semibold text-highlighted">Configuration</p>
              </div>
            </template>

            <div
              v-if="!themeSettings?.hasAssignedTheme"
              class="text-sm text-muted"
            >
              Assign a theme to edit workspace-specific theme settings.
            </div>

            <div
              v-else-if="themeSettings.fields.length === 0"
              class="text-sm text-muted"
            >
              The assigned theme currently has no configurable fields in <code>theme.json</code>.
            </div>

            <div
              v-else
              class="space-y-4"
            >
              <UCard
                v-for="group in groupedThemeFields"
                :key="group.groupName"
              >
                <template #header>
                  <p class="text-sm font-semibold text-highlighted">{{ group.groupName }}</p>
                </template>

                <div class="grid gap-4 md:grid-cols-2">
                  <div
                    v-for="field in group.fields"
                    :key="field.settingKey"
                  >
                    <UFormField
                      :label="field.label"
                      :description="field.description || undefined"
                      :required="field.isRequired"
                    >
                      <USwitch
                        v-if="field.fieldType === 'boolean' || field.fieldType === 'bool' || field.fieldType === 'switch'"
                        :model-value="Boolean(getFieldValue(field))"
                        @update:model-value="setFieldValue(field, $event)"
                      />

                      <USelect
                        v-else-if="field.fieldType === 'select' || field.fieldType === 'single-select'"
                        :model-value="String(getFieldValue(field) ?? '')"
                        :items="parseOptions(field)"
                        @update:model-value="setFieldValue(field, $event)"
                      />

                      <UTextarea
                        v-else-if="field.fieldType === 'textarea' || field.fieldType === 'text-area' || field.fieldType === 'multiline' || field.fieldType === 'code' || field.fieldType === 'json'"
                        :model-value="String(getFieldValue(field) ?? '')"
                        :rows="4"
                        @update:model-value="setFieldValue(field, $event)"
                      />

                      <UInput
                        v-else-if="field.fieldType === 'number' || field.fieldType === 'int' || field.fieldType === 'float'"
                        :model-value="String(getFieldValue(field) ?? '')"
                        type="number"
                        @update:model-value="setFieldValue(field, Number($event))"
                      />

                      <UInput
                        v-else-if="field.fieldType === 'color'"
                        :model-value="String(getFieldValue(field) ?? '')"
                        type="color"
                        @update:model-value="setFieldValue(field, $event)"
                      />

                      <UInput
                        v-else
                        :model-value="String(getFieldValue(field) ?? '')"
                        @update:model-value="setFieldValue(field, $event)"
                      />
                    </UFormField>
                  </div>
                </div>
              </UCard>

              <div class="flex justify-end">
                <UButton
                  :loading="savingThemeSettings"
                  @click="saveThemeSettings"
                >
                  Save Theme Settings
                </UButton>
              </div>
            </div>
          </UCard>

          <UCard>
            <template #header>
              <div>
                <p class="text-xs text-muted uppercase">Theme Resolution</p>
                <p class="text-xl font-semibold text-highlighted">Effective Templates</p>
              </div>
            </template>

            <UTable
              :data="effectiveTemplates"
              :columns="effectiveColumns"
              :loading="loadingThemes"
              class="shrink-0"
              :ui="{
                base: 'table-fixed border-separate border-spacing-0',
                thead: '[&>tr]:bg-elevated/50 [&>tr]:after:content-none',
                tbody: '[&>tr]:last:[&>td]:border-b-0',
                th: 'py-2 first:rounded-l-lg last:rounded-r-lg border-y border-default first:border-l last:border-r',
                td: 'border-b border-default',
                separator: 'h-0'
              }"
            />
          </UCard>
        </div>
      </template>

      <template v-else>
        <UCard>
          <template #header>
            <div>
              <p class="text-xs text-muted uppercase">Analysis</p>
              <p class="text-xl font-semibold text-highlighted">Workspace Analytics</p>
            </div>
          </template>

          <UAlert
            color="info"
            variant="soft"
            icon="i-lucide-bar-chart-3"
            description="Analytics is not connected yet. This tab will show workspace KPI and usage metrics once telemetry endpoints are available."
          />
        </UCard>
      </template>
    </template>
  </UDashboardPanel>

  <UModal
    v-model:open="themePickerOpen"
    title="Select Theme"
    description="Assign an active workspace theme to this workspace"
  >
    <template #body>
      <UInput
        v-model="themeSearch"
        class="mb-4"
        icon="i-lucide-search"
        placeholder="Search themes..."
      />

      <div class="grid grid-cols-1 gap-3 md:grid-cols-2 max-h-96 overflow-auto">
        <UCard
          v-for="theme in themeCandidates"
          :key="theme.key"
          as="button"
          type="button"
          class="text-left"
          :ui="{
            root: selectedTheme === theme.key
              ? 'ring ring-primary bg-primary/5'
              : 'ring ring-default hover:bg-elevated/50'
          }"
          @click="selectedTheme = theme.key"
        >
          <div class="space-y-2">
            <div class="h-16 rounded-md bg-elevated/70" />
            <div>
              <p class="text-sm font-medium text-highlighted">{{ theme.displayName }}</p>
              <p class="text-xs text-muted">{{ theme.pluginId }}@{{ theme.version }}</p>
            </div>
            <p class="text-xs text-muted">{{ theme.definitions }} template definitions</p>
          </div>
        </UCard>
      </div>

      <div class="mt-4 flex justify-end gap-2">
        <UButton
          color="neutral"
          variant="subtle"
          @click="themePickerOpen = false"
        >
          Cancel
        </UButton>
        <UButton
          :disabled="!selectedTheme"
          :loading="assigningTheme"
          @click="assignTheme"
        >
          Assign Theme
        </UButton>
      </div>
    </template>
  </UModal>
</template>
