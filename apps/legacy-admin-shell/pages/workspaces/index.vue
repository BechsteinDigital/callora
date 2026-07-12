<script setup lang="ts">
import { h, resolveComponent } from 'vue';
import * as z from 'zod';
import type { DropdownMenuItem, FormSubmitEvent, TableColumn } from '@nuxt/ui';
import type {
  AdminWorkspace,
  UpsertAdminWorkspaceRequest
} from '~/types/admin-workspaces';

const { request, requestSafe } = useAdminApi();

const channels = ref<AdminWorkspace[]>([]);
const loading = ref(true);
const listError = ref<string | null>(null);
const refreshedAt = ref<Date | null>(null);

const createOpen = ref(false);
const viewOpen = ref(false);
const editOpen = ref(false);
const deleteOpen = ref(false);

const isCreating = ref(false);
const isSaving = ref(false);
const isDeleting = ref(false);

const createError = ref<string | null>(null);
const editError = ref<string | null>(null);
const deleteError = ref<string | null>(null);

const activeChannelKey = ref<string | null>(null);
const query = ref('');

const schema = z.object({
  workspaceKey: z.string().min(1, 'Key is required'),
  displayName: z.string().min(1, 'Display name is required'),
  workspaceType: z.string().min(1, 'Type is required'),
  isActive: z.boolean(),
  publicBaseUrl: z.string().optional()
});

type WorkspaceSchema = z.output<typeof schema>;

const createState = reactive<WorkspaceSchema>({
  workspaceKey: '',
  displayName: '',
  workspaceType: 'voice',
  isActive: true,
  publicBaseUrl: ''
});

const editState = reactive<WorkspaceSchema>({
  workspaceKey: '',
  displayName: '',
  workspaceType: 'voice',
  isActive: true,
  publicBaseUrl: ''
});

const activeChannel = computed(() => {
  if (!activeChannelKey.value) {
    return null;
  }

  return channels.value.find((channel) => channel.workspaceKey === activeChannelKey.value) ?? null;
});

const filteredChannels = computed(() => {
  const value = query.value.trim().toLowerCase();
  if (!value) {
    return channels.value;
  }

  return channels.value.filter((channel) => {
    return channel.workspaceKey.toLowerCase().includes(value) ||
      channel.displayName.toLowerCase().includes(value) ||
      channel.tenantKey.toLowerCase().includes(value) ||
      channel.workspaceType.toLowerCase().includes(value);
  });
});

function toLocalDateTime(value: string | null): string {
  if (!value) {
    return '-';
  }

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

async function loadChannels(): Promise<void> {
  loading.value = true;
  listError.value = null;

  const channelsResult = await requestSafe<AdminWorkspace[]>('/api/workspaces');
  channels.value = channelsResult.ok ? (channelsResult.data ?? []) : [];

  if (!channelsResult.ok) {
    listError.value = 'Workspaces could not be loaded.';
  }

  if (activeChannelKey.value && !channels.value.some((channel) => channel.workspaceKey === activeChannelKey.value)) {
    activeChannelKey.value = null;
    viewOpen.value = false;
    editOpen.value = false;
    deleteOpen.value = false;
  }

  refreshedAt.value = new Date();
  loading.value = false;
}

function openCreateModal(): void {
  createError.value = null;
  createState.workspaceKey = '';
  createState.displayName = '';
  createState.workspaceType = 'voice';
  createState.isActive = true;
  createState.publicBaseUrl = '';
  createOpen.value = true;
}

function openViewModal(channel: AdminWorkspace): void {
  activeChannelKey.value = channel.workspaceKey;
  viewOpen.value = true;
}

function openEditModal(channel: AdminWorkspace): void {
  activeChannelKey.value = channel.workspaceKey;
  editError.value = null;
  editState.workspaceKey = channel.workspaceKey;
  editState.displayName = channel.displayName;
  editState.workspaceType = channel.workspaceType;
  editState.isActive = channel.isActive;
  editState.publicBaseUrl = channel.publicBaseUrl || '';
  editOpen.value = true;
}

function openDeleteModal(channel: AdminWorkspace): void {
  activeChannelKey.value = channel.workspaceKey;
  deleteError.value = null;
  deleteOpen.value = true;
}

async function createChannel(event: FormSubmitEvent<WorkspaceSchema>): Promise<void> {
  createError.value = null;
  isCreating.value = true;

  const payload: UpsertAdminWorkspaceRequest = {
    displayName: event.data.displayName.trim(),
    workspaceType: event.data.workspaceType.trim(),
    isActive: event.data.isActive,
    publicBaseUrl: event.data.publicBaseUrl?.trim() || null
  };

  try {
    await request<AdminWorkspace>(`/api/workspaces/${encodeURIComponent(event.data.workspaceKey.trim())}`, {
      method: 'PUT',
      body: payload
    });

    createOpen.value = false;
    await loadChannels();
  } catch (error) {
    createError.value = extractErrorMessage(error, 'Workspace could not be created.');
  } finally {
    isCreating.value = false;
  }
}

async function saveChannel(event: FormSubmitEvent<WorkspaceSchema>): Promise<void> {
  if (!activeChannel.value) {
    return;
  }

  editError.value = null;
  isSaving.value = true;

  const payload: UpsertAdminWorkspaceRequest = {
    displayName: event.data.displayName.trim(),
    workspaceType: event.data.workspaceType.trim(),
    isActive: event.data.isActive,
    publicBaseUrl: event.data.publicBaseUrl?.trim() || null
  };

  try {
    await request<AdminWorkspace>(`/api/workspaces/${encodeURIComponent(activeChannel.value.workspaceKey)}`, {
      method: 'PUT',
      body: payload
    });

    editOpen.value = false;
    await loadChannels();
  } catch (error) {
    editError.value = extractErrorMessage(error, 'Workspace could not be saved.');
  } finally {
    isSaving.value = false;
  }
}

async function confirmDelete(): Promise<void> {
  if (!activeChannel.value) {
    return;
  }

  deleteError.value = null;
  isDeleting.value = true;

  try {
    await request<void>(`/api/workspaces/${encodeURIComponent(activeChannel.value.workspaceKey)}`, {
      method: 'DELETE'
    });

    deleteOpen.value = false;
    await loadChannels();
  } catch (error) {
    deleteError.value = extractErrorMessage(error, 'Workspace could not be deleted.');
  } finally {
    isDeleting.value = false;
  }
}

function getRowItems(channel: AdminWorkspace): DropdownMenuItem[] {
  return [{
    type: 'label',
    label: channel.workspaceKey
  }, {
    label: 'Open workspace settings',
    icon: 'i-lucide-settings',
    onSelect: async () => {
      await navigateTo(`/workspaces/${encodeURIComponent(channel.workspaceKey)}`);
    }
  }, {
    label: 'View workspace',
    icon: 'i-lucide-eye',
    onSelect: () => {
      openViewModal(channel);
    }
  }, {
    label: 'Edit workspace',
    icon: 'i-lucide-pencil',
    onSelect: () => {
      openEditModal(channel);
    }
  }, {
    type: 'separator'
  }, {
    label: 'Delete workspace',
    icon: 'i-lucide-trash',
    color: 'error',
    onSelect: () => {
      openDeleteModal(channel);
    }
  }];
}

const UBadge = resolveComponent('UBadge');
const UButton = resolveComponent('UButton');
const UDropdownMenu = resolveComponent('UDropdownMenu');

const columns: TableColumn<AdminWorkspace>[] = [{
  accessorKey: 'workspaceKey',
  header: 'Key'
}, {
  accessorKey: 'displayName',
  header: 'Name'
}, {
  accessorKey: 'workspaceType',
  header: 'Type',
  cell: ({ row }) => h(UBadge, { color: 'neutral', variant: 'subtle', class: 'capitalize' }, () => row.original.workspaceType)
}, {
  accessorKey: 'publicBaseUrl',
  header: 'Public URL',
  cell: ({ row }) => row.original.publicBaseUrl || '-'
}, {
  accessorKey: 'tenantKey',
  header: 'Tenant'
}, {
  accessorKey: 'isActive',
  header: 'Status',
  cell: ({ row }) => {
    const color = row.original.isActive ? 'success' : 'warning';
    const label = row.original.isActive ? 'active' : 'inactive';
    return h(UBadge, { color, variant: 'subtle', class: 'capitalize' }, () => label);
  }
}, {
  id: 'actions',
  header: () => h('div', { class: 'text-right' }, 'Actions'),
  cell: ({ row }) => h(
    'div',
    { class: 'text-right' },
    h(
      UDropdownMenu,
      {
        content: { align: 'end' },
        items: getRowItems(row.original)
      },
      () => h(UButton, {
        icon: 'i-lucide-ellipsis-vertical',
        color: 'neutral',
        variant: 'ghost',
        class: 'ml-auto'
      })
    )
  )
}];

await loadChannels();
</script>

<template>
  <UDashboardPanel id="workspaces">
    <template #header>
      <UDashboardNavbar title="Workspaces">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>

        <template #right>
          <UButton
            label="New workspace"
            icon="i-lucide-plus"
            @click="openCreateModal"
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
      <div class="flex flex-wrap items-center justify-between gap-1.5">
        <UInput
          v-model="query"
          class="max-w-sm"
          icon="i-lucide-search"
          placeholder="Search workspaces..."
        />

        <UButton
          color="neutral"
          variant="outline"
          icon="i-lucide-refresh-cw"
          :loading="loading"
          @click="loadChannels"
        >
          Refresh
        </UButton>
      </div>

      <UAlert
        v-if="listError"
        color="error"
        variant="soft"
        :description="listError"
        icon="i-lucide-triangle-alert"
      />

      <UTable
        :data="filteredChannels"
        :columns="columns"
        :loading="loading"
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
    </template>
  </UDashboardPanel>

  <UModal
    v-model:open="createOpen"
    title="New workspace"
    description="Create a new workspace workspace"
  >
    <template #body>
      <UAlert
        v-if="createError"
        class="mb-4"
        color="error"
        variant="soft"
        :description="createError"
        icon="i-lucide-triangle-alert"
      />

      <UForm
        :schema="schema"
        :state="createState"
        class="space-y-4"
        @submit="createChannel"
      >
        <UFormField label="Workspace Key" name="workspaceKey">
          <UInput v-model="createState.workspaceKey" class="w-full" />
        </UFormField>

        <UFormField label="Name" name="displayName">
          <UInput v-model="createState.displayName" class="w-full" />
        </UFormField>

        <UFormField label="Type" name="workspaceType">
          <UInput v-model="createState.workspaceType" class="w-full" />
        </UFormField>

        <UFormField
          label="Public URL"
          name="publicBaseUrl"
          description="Examples: dialer.example.de or localhost/dialer"
        >
          <UInput v-model="createState.publicBaseUrl" class="w-full" placeholder="dialer.example.de" />
        </UFormField>

        <UFormField label="Active" name="isActive">
          <USwitch v-model="createState.isActive" />
        </UFormField>

        <div class="flex justify-end gap-2">
          <UButton
            label="Cancel"
            color="neutral"
            variant="subtle"
            @click="createOpen = false"
          />
          <UButton
            label="Create"
            type="submit"
            :loading="isCreating"
          />
        </div>
      </UForm>
    </template>
  </UModal>

  <UModal
    v-model:open="viewOpen"
    title="Workspace details"
    description="Read-only workspace information"
  >
    <template #body>
      <div
        v-if="activeChannel"
        class="space-y-3"
      >
        <UFormField label="Workspace Key">
          <UInput :model-value="activeChannel.workspaceKey" disabled />
        </UFormField>

        <UFormField label="Name">
          <UInput :model-value="activeChannel.displayName" disabled />
        </UFormField>

        <UFormField label="Tenant">
          <UInput :model-value="activeChannel.tenantKey" disabled />
        </UFormField>

        <UFormField label="Type">
          <UInput :model-value="activeChannel.workspaceType" disabled />
        </UFormField>

        <UFormField label="Public URL">
          <UInput :model-value="activeChannel.publicBaseUrl || '-'" disabled />
        </UFormField>

        <UFormField label="Resolved Route">
          <UInput :model-value="`${activeChannel.publicHost || '*'}${activeChannel.publicPathPrefix}`" disabled />
        </UFormField>

        <UFormField label="Status">
          <UInput :model-value="activeChannel.isActive ? 'Active' : 'Inactive'" disabled />
        </UFormField>

        <UFormField label="Theme">
          <UInput :model-value="activeChannel.themePluginId ? `${activeChannel.themePluginId}@${activeChannel.themeVersion || '-'}` : '-'" disabled />
        </UFormField>

        <UFormField label="Updated">
          <UInput :model-value="toLocalDateTime(activeChannel.updatedAtUtc)" disabled />
        </UFormField>
      </div>
    </template>
  </UModal>

  <UModal
    v-model:open="editOpen"
    title="Edit workspace"
    description="Update workspace settings"
  >
    <template #body>
      <UAlert
        v-if="editError"
        class="mb-4"
        color="error"
        variant="soft"
        :description="editError"
        icon="i-lucide-triangle-alert"
      />

      <UForm
        :schema="schema"
        :state="editState"
        class="space-y-4"
        @submit="saveChannel"
      >
        <UFormField label="Workspace Key" name="workspaceKey">
          <UInput v-model="editState.workspaceKey" class="w-full" disabled />
        </UFormField>

        <UFormField label="Name" name="displayName">
          <UInput v-model="editState.displayName" class="w-full" />
        </UFormField>

        <UFormField label="Type" name="workspaceType">
          <UInput v-model="editState.workspaceType" class="w-full" />
        </UFormField>

        <UFormField
          label="Public URL"
          name="publicBaseUrl"
          description="Examples: dialer.example.de or localhost/dialer"
        >
          <UInput v-model="editState.publicBaseUrl" class="w-full" placeholder="dialer.example.de" />
        </UFormField>

        <UFormField label="Active" name="isActive">
          <USwitch v-model="editState.isActive" />
        </UFormField>

        <div class="flex justify-end gap-2">
          <UButton
            label="Cancel"
            color="neutral"
            variant="subtle"
            @click="editOpen = false"
          />
          <UButton
            label="Save"
            type="submit"
            :loading="isSaving"
          />
        </div>
      </UForm>
    </template>
  </UModal>

  <UModal
    v-model:open="deleteOpen"
    :title="`Delete ${activeChannel?.workspaceKey || 'workspace'}`"
    description="Are you sure? This action cannot be undone."
  >
    <template #body>
      <UAlert
        v-if="deleteError"
        class="mb-4"
        color="error"
        variant="soft"
        :description="deleteError"
        icon="i-lucide-triangle-alert"
      />

      <div class="flex justify-end gap-2">
        <UButton
          label="Cancel"
          color="neutral"
          variant="subtle"
          @click="deleteOpen = false"
        />
        <UButton
          label="Delete"
          color="error"
          :loading="isDeleting"
          @click="confirmDelete"
        />
      </div>
    </template>
  </UModal>
</template>
