<script setup lang="ts">
import type { PluginEntitlementStatus, PluginInstallationSummary, PluginLifecycleApiResponse } from '~/types/admin-plugins';

const props = defineProps<{
  plugins: PluginInstallationSummary[];
  workspaceOptions: Array<{ label: string; value: string }>;
}>();

const emit = defineEmits<{
  changed: [];
}>();

const auth = useAdminAuth();
const { request, requestSafe } = useAdminApi();

const selectedWorkspaceKey = ref('');
const assignmentStates = ref<Record<string, boolean>>({});
const assignmentLoading = ref(false);
const mutatingPluginId = ref<string | null>(null);
const assignmentError = ref<string | null>(null);

const rows = computed(() => props.plugins.map((plugin) => ({
  pluginId: plugin.pluginId,
  displayName: plugin.displayName,
  state: plugin.state,
  isEntitled: assignmentStates.value[plugin.pluginId] ?? false
})));

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

function extractErrorMessage(error: unknown, fallback: string): string {
  const payload = (error as { data?: { message?: unknown } } | null)?.data;
  if (payload && typeof payload.message === 'string' && payload.message.trim().length > 0) {
    return payload.message;
  }

  return fallback;
}

async function loadAssignments(): Promise<void> {
  assignmentError.value = null;
  assignmentStates.value = {};

  const workspaceKey = selectedWorkspaceKey.value.trim();
  if (!workspaceKey || props.plugins.length === 0) {
    return;
  }

  assignmentLoading.value = true;
  const failures: string[] = [];
  const entries = await Promise.all(props.plugins.map(async (plugin) => {
    const response = await requestSafe<PluginEntitlementStatus>(
      `/api/plugins/workspaces/${encodeURIComponent(workspaceKey)}/entitlements/${encodeURIComponent(plugin.pluginId)}`
    );

    if (!response.ok || !response.data) {
      failures.push(plugin.pluginId);
      return [plugin.pluginId, false] as const;
    }

    return [plugin.pluginId, response.data.isEntitled] as const;
  }));

  assignmentStates.value = Object.fromEntries(entries);
  if (failures.length > 0) {
    assignmentError.value = `Workspace-Zuweisung konnte nicht vollständig geladen werden: ${failures.join(', ')}`;
  }
  assignmentLoading.value = false;
}

async function setAssignment(pluginId: string, isEnabled: boolean): Promise<void> {
  assignmentError.value = null;
  const workspaceKey = selectedWorkspaceKey.value.trim();
  if (!workspaceKey) {
    assignmentError.value = 'Workspace muss ausgewählt sein.';
    return;
  }

  mutatingPluginId.value = pluginId;
  try {
    const result = await request<PluginLifecycleApiResponse>(
      `/api/plugins/${encodeURIComponent(pluginId)}/${isEnabled ? 'activate' : 'deactivate'}`,
      {
        method: 'POST',
        body: {
          requestedBy: auth.session.value?.userId || null,
          workspaceKey
        }
      }
    );

    if (!result.isSuccess) {
      assignmentError.value = result.message || 'Workspace-Zuweisung fehlgeschlagen.';
      return;
    }

    await loadAssignments();
    emit('changed');
  } catch (error) {
    assignmentError.value = extractErrorMessage(error, 'Workspace-Zuweisung fehlgeschlagen.');
  } finally {
    mutatingPluginId.value = null;
  }
}

watch(selectedWorkspaceKey, () => {
  void loadAssignments();
});

watch(() => props.plugins, () => {
  void loadAssignments();
});

watch(() => props.workspaceOptions, (options) => {
  if (!selectedWorkspaceKey.value && options[0]) {
    selectedWorkspaceKey.value = options[0].value;
  }
}, { immediate: true });
</script>

<template>
  <UPageCard title="Workspace-Zuweisung">
    <div class="space-y-4">
      <div class="grid grid-cols-1 md:grid-cols-[280px_1fr] gap-4">
        <UFormField label="Workspace" required>
          <USelect
            v-model="selectedWorkspaceKey"
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
        v-if="assignmentError"
        color="error"
        variant="subtle"
        icon="i-lucide-triangle-alert"
        :title="assignmentError"
      />

      <UAlert
        v-if="mutatingPluginId"
        color="neutral"
        variant="subtle"
        icon="i-lucide-loader-circle"
        :title="`Workspace-Zuweisung für ${mutatingPluginId} wird aktualisiert...`"
      />

      <UEmpty
        v-if="workspaceOptions.length === 0"
        icon="i-lucide-store"
        title="Keine Workspaces vorhanden"
        description="Lege zuerst einen Workspace an, um Plugin-Zuweisungen zu konfigurieren."
      />

      <UEmpty
        v-else-if="rows.length === 0"
        icon="i-lucide-plug-zap"
        title="Keine Plugins installiert"
        description="Installiere zuerst ein Plugin, um es einem Workspace zuzuweisen."
      />

      <div v-else class="grid grid-cols-1 xl:grid-cols-2 gap-4">
        <UCard v-for="item in rows" :key="item.pluginId">
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
                :loading="assignmentLoading || mutatingPluginId === item.pluginId"
                :disabled="item.state === 3"
                @click="setAssignment(item.pluginId, true)"
              >
                Aktivieren
              </UButton>
              <UButton
                color="warning"
                variant="soft"
                icon="i-lucide-circle-off"
                :loading="assignmentLoading || mutatingPluginId === item.pluginId"
                :disabled="item.state === 3"
                @click="setAssignment(item.pluginId, false)"
              >
                Deaktivieren
              </UButton>
            </div>
          </div>
        </UCard>
      </div>
    </div>
  </UPageCard>
</template>
