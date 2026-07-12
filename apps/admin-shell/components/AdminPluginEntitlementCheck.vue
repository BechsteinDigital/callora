<script setup lang="ts">
import type { PluginEntitlementStatus } from '~/types/admin-plugins';

const props = defineProps<{
  pluginOptions: Array<{ label: string; value: string }>;
  workspaceOptions: Array<{ label: string; value: string }>;
  tenantOptions: Array<{ label: string; value: string }>;
}>();

const { requestSafe } = useAdminApi();

const entitlementState = reactive({
  pluginId: '',
  workspaceKey: '',
  tenantId: ''
});

const workspaceResult = ref<PluginEntitlementStatus | null>(null);
const tenantResult = ref<PluginEntitlementStatus | null>(null);
const entitlementError = ref<string | null>(null);
const workspaceLoading = ref(false);
const tenantLoading = ref(false);

watch(() => [props.pluginOptions, props.workspaceOptions, props.tenantOptions], () => {
  if (!entitlementState.pluginId && props.pluginOptions[0]) {
    entitlementState.pluginId = props.pluginOptions[0].value;
  }

  if (!entitlementState.workspaceKey && props.workspaceOptions[0]) {
    entitlementState.workspaceKey = props.workspaceOptions[0].value;
  }

  if (!entitlementState.tenantId && props.tenantOptions[0]) {
    entitlementState.tenantId = props.tenantOptions[0].value;
  }
}, { immediate: true, deep: true });

async function checkWorkspaceEntitlement(): Promise<void> {
  entitlementError.value = null;
  workspaceResult.value = null;

  if (!entitlementState.pluginId || !entitlementState.workspaceKey) {
    entitlementError.value = 'Plugin und Workspace müssen ausgewählt sein.';
    return;
  }

  workspaceLoading.value = true;

  const response = await requestSafe<PluginEntitlementStatus>(
    `/api/plugins/workspaces/${encodeURIComponent(entitlementState.workspaceKey)}/entitlements/${encodeURIComponent(entitlementState.pluginId)}`
  );

  if (response.ok && response.data) {
    workspaceResult.value = response.data;
  } else {
    entitlementError.value = 'Workspace-Entitlement konnte nicht geladen werden.';
  }

  workspaceLoading.value = false;
}

async function checkTenantEntitlement(): Promise<void> {
  entitlementError.value = null;
  tenantResult.value = null;

  if (!entitlementState.pluginId || !entitlementState.tenantId) {
    entitlementError.value = 'Plugin und Tenant müssen ausgewählt sein.';
    return;
  }

  tenantLoading.value = true;

  const response = await requestSafe<PluginEntitlementStatus>(
    `/api/plugins/tenants/${encodeURIComponent(entitlementState.tenantId)}/entitlements/${encodeURIComponent(entitlementState.pluginId)}`
  );

  if (response.ok && response.data) {
    tenantResult.value = response.data;
  } else {
    entitlementError.value = 'Tenant-Entitlement konnte nicht geladen werden.';
  }

  tenantLoading.value = false;
}
</script>

<template>
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
            :loading="workspaceLoading"
            @click="checkWorkspaceEntitlement"
          >
            Prüfen
          </UButton>

          <UAlert
            v-if="workspaceResult"
            :color="workspaceResult.isEntitled ? 'success' : 'warning'"
            variant="subtle"
            icon="i-lucide-shield-check"
            :title="workspaceResult.isEntitled ? 'Entitled' : 'Nicht entitled'"
            :description="`Plugin ${workspaceResult.pluginId} für Workspace ${workspaceResult.workspaceKey}`"
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
            :loading="tenantLoading"
            @click="checkTenantEntitlement"
          >
            Prüfen
          </UButton>

          <UAlert
            v-if="tenantResult"
            :color="tenantResult.isEntitled ? 'success' : 'warning'"
            variant="subtle"
            icon="i-lucide-shield-check"
            :title="tenantResult.isEntitled ? 'Entitled' : 'Nicht entitled'"
            :description="`Plugin ${tenantResult.pluginId} für Tenant ${tenantResult.workspaceKey}`"
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
</template>
