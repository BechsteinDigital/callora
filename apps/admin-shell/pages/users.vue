<script setup lang="ts">
import { h, resolveComponent } from 'vue';
import * as z from 'zod';
import type { DropdownMenuItem, FormSubmitEvent, TableColumn } from '@nuxt/ui';
import type { CreateAdminUserRequest, AdminUser, UpdateAdminUserRequest } from '~/types/admin-users';

const { request, requestSafe } = useAdminApi();

const users = ref<AdminUser[]>([]);
const loading = ref(true);
const refreshedAt = ref<Date | null>(null);
const listError = ref<string | null>(null);

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

const activeUserId = ref<string | null>(null);
const emailFilter = ref('');

const createSchema = z.object({
  externalId: z.string().min(1, 'User ID is required'),
  email: z.string().email('Invalid email').or(z.literal('')),
  displayName: z.string(),
  password: z.string().min(6, 'Password must be at least 6 characters')
});

const editSchema = z.object({
  email: z.string().email('Invalid email').or(z.literal('')),
  displayName: z.string(),
  password: z.string()
});

type CreateSchema = z.output<typeof createSchema>;
type EditSchema = z.output<typeof editSchema>;

const createState = reactive<CreateSchema>({
  externalId: '',
  email: '',
  displayName: '',
  password: ''
});

const editState = reactive<EditSchema>({
  email: '',
  displayName: '',
  password: ''
});

const activeUser = computed(() => {
  if (!activeUserId.value) {
    return null;
  }

  return users.value.find((user) => user.externalId === activeUserId.value) ?? null;
});

const filteredUsers = computed(() => {
  const value = emailFilter.value.trim().toLowerCase();
  if (!value) {
    return users.value;
  }

  return users.value.filter((user) => (user.email ?? '').toLowerCase().includes(value));
});

function toNullable(value: string): string | null {
  const normalized = value.trim();
  return normalized.length > 0 ? normalized : null;
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

async function loadUsers(): Promise<void> {
  loading.value = true;
  listError.value = null;

  const response = await requestSafe<AdminUser[]>('/api/users');
  if (response.ok) {
    users.value = response.data ?? [];
    if (activeUserId.value && !users.value.some((user) => user.externalId === activeUserId.value)) {
      activeUserId.value = null;
      viewOpen.value = false;
      editOpen.value = false;
      deleteOpen.value = false;
    }
  } else {
    users.value = [];
    listError.value = 'Users could not be loaded.';
  }

  refreshedAt.value = new Date();
  loading.value = false;
}

function openCreateModal(): void {
  createError.value = null;
  createState.externalId = '';
  createState.email = '';
  createState.displayName = '';
  createState.password = '';
  createOpen.value = true;
}

function openViewModal(user: AdminUser): void {
  activeUserId.value = user.externalId;
  viewOpen.value = true;
}

function openEditModal(user: AdminUser): void {
  activeUserId.value = user.externalId;
  editError.value = null;
  editState.email = user.email ?? '';
  editState.displayName = user.displayName ?? '';
  editState.password = '';
  editOpen.value = true;
}

function openDeleteModal(user: AdminUser): void {
  activeUserId.value = user.externalId;
  deleteError.value = null;
  deleteOpen.value = true;
}

async function onCreateSubmit(event: FormSubmitEvent<CreateSchema>): Promise<void> {
  createError.value = null;
  isCreating.value = true;

  const payload: CreateAdminUserRequest = {
    externalId: event.data.externalId.trim(),
    email: toNullable(event.data.email),
    displayName: toNullable(event.data.displayName),
    password: event.data.password
  };

  try {
    await request<AdminUser>('/api/users', {
      method: 'POST',
      body: payload
    });

    createOpen.value = false;
    await loadUsers();
  } catch (error) {
    createError.value = extractErrorMessage(error, 'User could not be created.');
  } finally {
    isCreating.value = false;
  }
}

async function onEditSubmit(event: FormSubmitEvent<EditSchema>): Promise<void> {
  if (!activeUser.value) {
    return;
  }

  editError.value = null;
  isSaving.value = true;

  const payload: UpdateAdminUserRequest = {
    email: toNullable(event.data.email),
    displayName: toNullable(event.data.displayName),
    password: toNullable(event.data.password)
  };

  try {
    await request<AdminUser>(`/api/users/${encodeURIComponent(activeUser.value.externalId)}`, {
      method: 'PUT',
      body: payload
    });

    editOpen.value = false;
    await loadUsers();
  } catch (error) {
    editError.value = extractErrorMessage(error, 'User could not be saved.');
  } finally {
    isSaving.value = false;
  }
}

async function confirmDelete(): Promise<void> {
  if (!activeUser.value) {
    return;
  }

  deleteError.value = null;
  isDeleting.value = true;

  try {
    await request<void>(`/api/users/${encodeURIComponent(activeUser.value.externalId)}`, {
      method: 'DELETE'
    });

    deleteOpen.value = false;
    await loadUsers();
  } catch (error) {
    deleteError.value = extractErrorMessage(error, 'User could not be deleted.');
  } finally {
    isDeleting.value = false;
  }
}

function getRowItems(user: AdminUser): DropdownMenuItem[] {
  return [{
    type: 'label',
    label: user.externalId
  }, {
    label: 'View user',
    icon: 'i-lucide-eye',
    onSelect: () => {
      openViewModal(user);
    }
  }, {
    label: 'Edit user',
    icon: 'i-lucide-pencil',
    onSelect: () => {
      openEditModal(user);
    }
  }, {
    type: 'separator'
  }, {
    label: 'Delete user',
    icon: 'i-lucide-trash',
    color: 'error',
    onSelect: () => {
      openDeleteModal(user);
    }
  }];
}

const UBadge = resolveComponent('UBadge');
const UButton = resolveComponent('UButton');
const UDropdownMenu = resolveComponent('UDropdownMenu');

const columns: TableColumn<AdminUser>[] = [{
  accessorKey: 'externalId',
  header: 'User ID'
}, {
  accessorKey: 'displayName',
  header: 'Display Name',
  cell: ({ row }) => row.original.displayName || '-'
}, {
  accessorKey: 'email',
  header: 'Email',
  cell: ({ row }) => row.original.email || '-'
}, {
  accessorKey: 'hasPassword',
  header: 'Password',
  cell: ({ row }) => {
    const color = row.original.hasPassword ? 'success' : 'warning';
    const label = row.original.hasPassword ? 'set' : 'missing';
    return h(UBadge, { color, variant: 'subtle', class: 'capitalize' }, () => label);
  }
}, {
  accessorKey: 'updatedAtUtc',
  header: 'Updated',
  cell: ({ row }) => toLocalDateTime(row.original.updatedAtUtc)
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

await loadUsers();
</script>

<template>
  <UDashboardPanel id="users">
    <template #header>
      <UDashboardNavbar title="Users">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>

        <template #right>
          <UButton
            label="New user"
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
          v-model="emailFilter"
          class="max-w-sm"
          icon="i-lucide-search"
          placeholder="Filter emails..."
        />

        <UButton
          color="neutral"
          variant="outline"
          icon="i-lucide-refresh-cw"
          :loading="loading"
          @click="loadUsers"
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
        :data="filteredUsers"
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
    title="New user"
    description="Create a new admin user account"
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
        :schema="createSchema"
        :state="createState"
        class="space-y-4"
        @submit="onCreateSubmit"
      >
        <UFormField label="User ID" name="externalId">
          <UInput v-model="createState.externalId" class="w-full" />
        </UFormField>

        <UFormField label="Display Name" name="displayName">
          <UInput v-model="createState.displayName" class="w-full" />
        </UFormField>

        <UFormField label="Email" name="email">
          <UInput v-model="createState.email" class="w-full" type="email" />
        </UFormField>

        <UFormField label="Password" name="password">
          <UInput v-model="createState.password" class="w-full" type="password" />
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
    title="User details"
    description="Read-only user information"
  >
    <template #body>
      <div
        v-if="activeUser"
        class="space-y-3"
      >
        <UFormField label="User ID">
          <UInput :model-value="activeUser.externalId" disabled />
        </UFormField>

        <UFormField label="Display Name">
          <UInput :model-value="activeUser.displayName || '-'" disabled />
        </UFormField>

        <UFormField label="Email">
          <UInput :model-value="activeUser.email || '-'" disabled />
        </UFormField>

        <UFormField label="Password">
          <UInput :model-value="activeUser.hasPassword ? 'Set' : 'Missing'" disabled />
        </UFormField>

        <UFormField label="Created">
          <UInput :model-value="toLocalDateTime(activeUser.createdAtUtc)" disabled />
        </UFormField>

        <UFormField label="Updated">
          <UInput :model-value="toLocalDateTime(activeUser.updatedAtUtc)" disabled />
        </UFormField>
      </div>
    </template>
  </UModal>

  <UModal
    v-model:open="editOpen"
    title="Edit user"
    description="Update user profile and credentials"
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
        :schema="editSchema"
        :state="editState"
        class="space-y-4"
        @submit="onEditSubmit"
      >
        <UFormField label="User ID">
          <UInput :model-value="activeUser?.externalId || ''" class="w-full" disabled />
        </UFormField>

        <UFormField label="Display Name" name="displayName">
          <UInput v-model="editState.displayName" class="w-full" />
        </UFormField>

        <UFormField label="Email" name="email">
          <UInput v-model="editState.email" class="w-full" type="email" />
        </UFormField>

        <UFormField
          label="New Password"
          name="password"
          description="Leave empty to keep existing password"
        >
          <UInput v-model="editState.password" class="w-full" type="password" />
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
    :title="`Delete ${activeUser?.externalId || 'user'}`"
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
