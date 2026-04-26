<script setup lang="ts">
import { h, resolveComponent } from 'vue';
import type { DropdownMenuItem, TableColumn } from '@nuxt/ui';
import type { AdminUser } from '~/types/admin-users';
import type {
  RbacPermission,
  RbacRole,
  RbacUserRoleAssignment,
  UpsertRoleRequest,
  UpsertUserRoleRequest
} from '~/types/admin-rbac';

const { request, requestSafe } = useAdminApi();

const roles = ref<RbacRole[]>([]);
const assignments = ref<RbacUserRoleAssignment[]>([]);
const permissions = ref<RbacPermission[]>([]);
const users = ref<AdminUser[]>([]);
const loading = ref(true);
const error = ref<string | null>(null);

const roleModalOpen = ref(false);
const assignmentModalOpen = ref(false);
const roleDeleteOpen = ref(false);
const assignmentDeleteOpen = ref(false);

const activeRole = ref<string | null>(null);
const activeUserId = ref<string | null>(null);

const roleForm = reactive({
  role: '',
  permissionKeys: [] as string[]
});

const assignmentForm = reactive({
  userId: '',
  role: ''
});

const saving = ref(false);

const roleOptions = computed(() =>
  roles.value.map((role) => ({ label: role.role, value: role.role })));

const userOptions = computed(() =>
  users.value.map((user) => ({ label: user.displayName || user.externalId, value: user.externalId })));

const permissionOptions = computed(() =>
  permissions.value.map((permission) => ({
    label: permission.permissionKey,
    value: permission.permissionKey
  })));

function extractErrorMessage(errorValue: unknown, fallback: string): string {
  if (typeof errorValue !== 'object' || !errorValue) {
    return fallback;
  }

  const payload = (errorValue as { data?: { message?: unknown } }).data;
  if (payload && typeof payload.message === 'string' && payload.message.trim().length > 0) {
    return payload.message;
  }

  return fallback;
}

function permissionKeysToFunctions(permissionKeys: string[]): UpsertRoleRequest['functions'] {
  const byFunction = new Map<string, Set<string>>();

  for (const permissionKey of permissionKeys) {
    const [func, action] = permissionKey.split('.', 2);
    if (!func || !action) {
      continue;
    }

    if (!byFunction.has(func)) {
      byFunction.set(func, new Set<string>());
    }

    byFunction.get(func)!.add(action);
  }

  return Array.from(byFunction.entries())
    .sort((a, b) => a[0].localeCompare(b[0]))
    .map(([func, actions]) => ({
      function: func,
      actions: Array.from(actions).sort((a, b) => a.localeCompare(b))
    }));
}

async function loadAll(): Promise<void> {
  loading.value = true;
  error.value = null;

  const [rolesResult, assignmentsResult, permissionsResult, usersResult] = await Promise.all([
    requestSafe<RbacRole[]>('/api/security/rbac/roles'),
    requestSafe<RbacUserRoleAssignment[]>('/api/security/rbac/users'),
    requestSafe<RbacPermission[]>('/api/security/rbac/permissions'),
    requestSafe<AdminUser[]>('/api/users')
  ]);

  roles.value = rolesResult.ok ? (rolesResult.data ?? []) : [];
  assignments.value = assignmentsResult.ok ? (assignmentsResult.data ?? []) : [];
  permissions.value = permissionsResult.ok ? (permissionsResult.data ?? []) : [];
  users.value = usersResult.ok ? (usersResult.data ?? []) : [];

  if (!rolesResult.ok || !assignmentsResult.ok || !permissionsResult.ok || !usersResult.ok) {
    error.value = 'RBAC Daten konnten nicht vollständig geladen werden.';
  }

  loading.value = false;
}

function openCreateRole(): void {
  activeRole.value = null;
  roleForm.role = '';
  roleForm.permissionKeys = [];
  roleModalOpen.value = true;
}

function openEditRole(role: RbacRole): void {
  activeRole.value = role.role;
  roleForm.role = role.role;
  roleForm.permissionKeys = [...role.permissions];
  roleModalOpen.value = true;
}

function openDeleteRole(role: RbacRole): void {
  activeRole.value = role.role;
  roleDeleteOpen.value = true;
}

async function saveRole(): Promise<void> {
  const role = roleForm.role.trim();
  if (!role) {
    error.value = 'Rollenname ist erforderlich.';
    return;
  }

  saving.value = true;
  error.value = null;

  try {
    const payload: UpsertRoleRequest = {
      functions: permissionKeysToFunctions(roleForm.permissionKeys)
    };

    await request<RbacRole>(`/api/security/rbac/roles/${encodeURIComponent(role)}`, {
      method: 'PUT',
      body: payload
    });

    roleModalOpen.value = false;
    await loadAll();
  } catch (errorValue) {
    error.value = extractErrorMessage(errorValue, 'Rolle konnte nicht gespeichert werden.');
  } finally {
    saving.value = false;
  }
}

async function deleteRole(): Promise<void> {
  if (!activeRole.value) {
    return;
  }

  saving.value = true;
  error.value = null;
  try {
    await request<void>(`/api/security/rbac/roles/${encodeURIComponent(activeRole.value)}`, { method: 'DELETE' });
    roleDeleteOpen.value = false;
    await loadAll();
  } catch (errorValue) {
    error.value = extractErrorMessage(errorValue, 'Rolle konnte nicht gelöscht werden.');
  } finally {
    saving.value = false;
  }
}

function openCreateAssignment(): void {
  activeUserId.value = null;
  assignmentForm.userId = '';
  assignmentForm.role = '';
  assignmentModalOpen.value = true;
}

function openEditAssignment(assignment: RbacUserRoleAssignment): void {
  activeUserId.value = assignment.userId;
  assignmentForm.userId = assignment.userId;
  assignmentForm.role = assignment.role;
  assignmentModalOpen.value = true;
}

function openDeleteAssignment(assignment: RbacUserRoleAssignment): void {
  activeUserId.value = assignment.userId;
  assignmentDeleteOpen.value = true;
}

async function saveAssignment(): Promise<void> {
  const userId = assignmentForm.userId.trim();
  const role = assignmentForm.role.trim();
  if (!userId || !role) {
    error.value = 'User und Rolle sind erforderlich.';
    return;
  }

  saving.value = true;
  error.value = null;
  try {
    const payload: UpsertUserRoleRequest = { role };
    await request<RbacUserRoleAssignment>(`/api/security/rbac/users/${encodeURIComponent(userId)}`, {
      method: 'PUT',
      body: payload
    });

    assignmentModalOpen.value = false;
    await loadAll();
  } catch (errorValue) {
    error.value = extractErrorMessage(errorValue, 'Rollen-Zuweisung konnte nicht gespeichert werden.');
  } finally {
    saving.value = false;
  }
}

async function deleteAssignment(): Promise<void> {
  if (!activeUserId.value) {
    return;
  }

  saving.value = true;
  error.value = null;
  try {
    await request<void>(`/api/security/rbac/users/${encodeURIComponent(activeUserId.value)}`, { method: 'DELETE' });
    assignmentDeleteOpen.value = false;
    await loadAll();
  } catch (errorValue) {
    error.value = extractErrorMessage(errorValue, 'Rollen-Zuweisung konnte nicht gelöscht werden.');
  } finally {
    saving.value = false;
  }
}

function roleRowItems(role: RbacRole): DropdownMenuItem[] {
  return [{
    label: 'Bearbeiten',
    icon: 'i-lucide-pencil',
    onSelect: () => openEditRole(role)
  }, {
    label: 'Löschen',
    icon: 'i-lucide-trash',
    color: 'error',
    onSelect: () => openDeleteRole(role)
  }];
}

function assignmentRowItems(assignment: RbacUserRoleAssignment): DropdownMenuItem[] {
  return [{
    label: 'Bearbeiten',
    icon: 'i-lucide-pencil',
    onSelect: () => openEditAssignment(assignment)
  }, {
    label: 'Löschen',
    icon: 'i-lucide-trash',
    color: 'error',
    onSelect: () => openDeleteAssignment(assignment)
  }];
}

const UButton = resolveComponent('UButton');
const UDropdownMenu = resolveComponent('UDropdownMenu');

const roleColumns: TableColumn<RbacRole>[] = [{
  accessorKey: 'role',
  header: 'Rolle'
}, {
  accessorKey: 'permissions',
  header: 'Permissions',
  cell: ({ row }) => row.original.permissions.join(', ')
}, {
  id: 'actions',
  header: '',
  cell: ({ row }) => h('div', { class: 'text-right' }, [
    h(UDropdownMenu, {
      content: { align: 'end' },
      items: roleRowItems(row.original)
    }, () => h(UButton, { color: 'neutral', variant: 'ghost', icon: 'i-lucide-ellipsis' }))
  ])
}];

const assignmentColumns: TableColumn<RbacUserRoleAssignment>[] = [{
  accessorKey: 'userId',
  header: 'User'
}, {
  accessorKey: 'role',
  header: 'Rolle'
}, {
  id: 'actions',
  header: '',
  cell: ({ row }) => h('div', { class: 'text-right' }, [
    h(UDropdownMenu, {
      content: { align: 'end' },
      items: assignmentRowItems(row.original)
    }, () => h(UButton, { color: 'neutral', variant: 'ghost', icon: 'i-lucide-ellipsis' }))
  ])
}];

await loadAll();
</script>

<template>
  <UDashboardPanel id="rbac">
    <template #header>
      <UDashboardNavbar title="RBAC">
        <template #leading>
          <UDashboardSidebarCollapse />
        </template>
      </UDashboardNavbar>
      <UDashboardToolbar>
        <template #left>
          <UButton color="primary" icon="i-lucide-plus" @click="openCreateRole">Neue Rolle</UButton>
          <UButton color="neutral" variant="soft" icon="i-lucide-user-plus" @click="openCreateAssignment">User zuweisen</UButton>
        </template>
      </UDashboardToolbar>
    </template>

    <template #body>
      <UAlert v-if="error" color="error" variant="subtle" :title="error" />

      <UPageCard title="Rollen">
        <UTable :data="roles" :columns="roleColumns" :loading="loading" />
      </UPageCard>

      <UPageCard title="User Rollen-Zuweisung">
        <UTable :data="assignments" :columns="assignmentColumns" :loading="loading" />
      </UPageCard>
    </template>
  </UDashboardPanel>

  <UModal v-model:open="roleModalOpen" title="Rolle bearbeiten" :ui="{ footer: 'justify-end gap-2' }">
    <template #body>
      <div class="space-y-3">
        <UFormField label="Rollenname" required>
          <UInput v-model="roleForm.role" />
        </UFormField>
        <UFormField label="Permissions">
          <USelectMenu v-model="roleForm.permissionKeys" :items="permissionOptions" value-key="value" multiple class="w-full" />
        </UFormField>
      </div>
    </template>
    <template #footer>
      <UButton color="neutral" variant="ghost" @click="roleModalOpen = false">Abbrechen</UButton>
      <UButton color="primary" :loading="saving" @click="saveRole">Speichern</UButton>
    </template>
  </UModal>

  <UModal v-model:open="assignmentModalOpen" title="User Rolle zuweisen" :ui="{ footer: 'justify-end gap-2' }">
    <template #body>
      <div class="space-y-3">
        <UFormField label="User" required>
          <USelectMenu v-model="assignmentForm.userId" :items="userOptions" value-key="value" class="w-full" />
        </UFormField>
        <UFormField label="Rolle" required>
          <USelectMenu v-model="assignmentForm.role" :items="roleOptions" value-key="value" class="w-full" />
        </UFormField>
      </div>
    </template>
    <template #footer>
      <UButton color="neutral" variant="ghost" @click="assignmentModalOpen = false">Abbrechen</UButton>
      <UButton color="primary" :loading="saving" @click="saveAssignment">Speichern</UButton>
    </template>
  </UModal>

  <UModal v-model:open="roleDeleteOpen" title="Rolle löschen" :ui="{ footer: 'justify-end gap-2' }">
    <template #body>
      <p class="text-sm text-muted">Rolle {{ activeRole }} wirklich löschen?</p>
    </template>
    <template #footer>
      <UButton color="neutral" variant="ghost" @click="roleDeleteOpen = false">Abbrechen</UButton>
      <UButton color="error" :loading="saving" @click="deleteRole">Löschen</UButton>
    </template>
  </UModal>

  <UModal v-model:open="assignmentDeleteOpen" title="Zuweisung löschen" :ui="{ footer: 'justify-end gap-2' }">
    <template #body>
      <p class="text-sm text-muted">Zuweisung für User {{ activeUserId }} entfernen?</p>
    </template>
    <template #footer>
      <UButton color="neutral" variant="ghost" @click="assignmentDeleteOpen = false">Abbrechen</UButton>
      <UButton color="error" :loading="saving" @click="deleteAssignment">Löschen</UButton>
    </template>
  </UModal>
</template>
