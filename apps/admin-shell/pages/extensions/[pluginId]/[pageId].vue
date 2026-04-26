<script setup lang="ts">
import { h, resolveComponent } from "vue";
import type { TableColumn } from "@nuxt/ui";
import type { AdminPluginCrudField, AdminPluginCrudPageExtension } from "~/types/admin-plugin-extensions";

type CrudRow = Record<string, unknown>;

const route = useRoute();
const { request, requestSafe } = useAdminApi();
const { ensureAdminPluginAssetsLoaded } = useAdminPluginAssets();
const { findCrudPageByRoute } = useAdminPageExtensions();

const extension = ref<AdminPluginCrudPageExtension | null>(null);
const loadingExtension = ref(true);

const entries = ref<CrudRow[]>([]);
const loading = ref(true);
const saving = ref(false);
const listError = ref<string | null>(null);
const saveError = ref<string | null>(null);

const detailsModalOpen = ref(false);
const editModalOpen = ref(false);
const selectedEntry = ref<CrudRow | null>(null);
const editTargetId = ref<string | null>(null);
const formState = reactive<Record<string, unknown>>({});

function toSegment(value: unknown): string {
  if (Array.isArray(value)) {
    return String(value[0] ?? "");
  }

  return String(value ?? "");
}

const routePath = computed(() => `/extensions/${toSegment(route.params.pluginId)}/${toSegment(route.params.pageId)}`);

function clearFormState(): void {
  Object.keys(formState).forEach((key) => {
    delete formState[key];
  });
}

function isNonEmptyText(value: unknown): boolean {
  return String(value ?? "").trim().length > 0;
}

function toRowId(row: CrudRow): string {
  if (!extension.value) {
    return "";
  }

  return String(row[extension.value.primaryKey] ?? "");
}

function toDisplayValue(value: unknown): string {
  if (value === null || value === undefined) {
    return "-";
  }

  if (typeof value === "boolean") {
    return value ? "true" : "false";
  }

  return String(value);
}

function toDateTime(value: unknown): string {
  if (!value) {
    return "-";
  }

  const parsed = new Date(String(value));
  if (Number.isNaN(parsed.getTime())) {
    return String(value);
  }

  return parsed.toLocaleString();
}

function initializeFormForCreate(): void {
  clearFormState();
  if (!extension.value) {
    return;
  }

  extension.value.formFields.forEach((field) => {
    formState[field.key] = field.type === "boolean" ? false : "";
  });
}

function initializeFormForEdit(row: CrudRow): void {
  clearFormState();
  if (!extension.value) {
    return;
  }

  extension.value.formFields.forEach((field) => {
    if (field.type === "password") {
      formState[field.key] = "";
      return;
    }

    formState[field.key] = row[field.key] ?? (field.type === "boolean" ? false : "");
  });
}

function openCreate(): void {
  editTargetId.value = null;
  saveError.value = null;
  initializeFormForCreate();
  editModalOpen.value = true;
}

function openEdit(row: CrudRow): void {
  editTargetId.value = toRowId(row);
  saveError.value = null;
  initializeFormForEdit(row);
  editModalOpen.value = true;
}

function openDetails(row: CrudRow): void {
  selectedEntry.value = row;
  detailsModalOpen.value = true;
}

function isFieldRequired(field: AdminPluginCrudField): boolean {
  if (!field.required) {
    return false;
  }

  if (!editTargetId.value) {
    return true;
  }

  return !field.requiredOnCreateOnly;
}

function validateForm(): string | null {
  if (!extension.value) {
    return "Missing plugin page extension definition.";
  }

  for (const field of extension.value.formFields) {
    if (!isFieldRequired(field) || field.type === "boolean") {
      continue;
    }

    if (!isNonEmptyText(formState[field.key])) {
      return `${field.label} is required.`;
    }
  }

  return null;
}

function buildPayload(): Record<string, unknown> {
  const payload: Record<string, unknown> = {};
  if (!extension.value) {
    return payload;
  }

  for (const field of extension.value.formFields) {
    const currentValue = formState[field.key];

    if (field.type === "boolean") {
      payload[field.key] = Boolean(currentValue);
      continue;
    }

    const normalized = String(currentValue ?? "").trim();
    payload[field.key] = normalized;
  }

  return payload;
}

async function loadEntries(): Promise<void> {
  if (!extension.value) {
    entries.value = [];
    loading.value = false;
    listError.value = "No plugin page extension registered for this route.";
    return;
  }

  loading.value = true;
  listError.value = null;

  const response = await requestSafe<CrudRow[]>(extension.value.apiBasePath);
  if (!response.ok) {
    entries.value = [];
    listError.value = "Data could not be loaded.";
    loading.value = false;
    return;
  }

  entries.value = response.data ?? [];
  loading.value = false;
}

async function resolvePageExtension(): Promise<void> {
  loadingExtension.value = true;
  await ensureAdminPluginAssetsLoaded();
  extension.value = findCrudPageByRoute(routePath.value);
  loadingExtension.value = false;
}

async function save(): Promise<void> {
  if (!extension.value) {
    saveError.value = "No plugin extension definition found.";
    return;
  }

  const validationError = validateForm();
  if (validationError) {
    saveError.value = validationError;
    return;
  }

  saving.value = true;
  saveError.value = null;

  const payload = buildPayload();

  try {
    if (editTargetId.value) {
      await request(`${extension.value.apiBasePath}/${encodeURIComponent(editTargetId.value)}`, {
        method: "PUT",
        body: payload
      });
    } else {
      await request(extension.value.apiBasePath, {
        method: "POST",
        body: payload
      });
    }

    editModalOpen.value = false;
    await loadEntries();
  } catch (error) {
    const payloadError = (error as { data?: { message?: string } })?.data?.message;
    saveError.value = payloadError || "Saving failed.";
  } finally {
    saving.value = false;
  }
}

async function remove(row: CrudRow): Promise<void> {
  if (!extension.value) {
    return;
  }

  const itemId = toRowId(row);
  if (!itemId) {
    return;
  }

  const confirmed = globalThis.confirm(`Delete item '${itemId}'?`);
  if (!confirmed) {
    return;
  }

  await request(`${extension.value.apiBasePath}/${encodeURIComponent(itemId)}`, {
    method: "DELETE"
  });

  await loadEntries();
}

const tableColumns = computed<TableColumn<CrudRow>[]>(() => {
  if (!extension.value) {
    return [];
  }

  const configuredColumns: TableColumn<CrudRow>[] = extension.value.columns.map((column) => ({
    id: column.key,
    accessorKey: column.key,
    header: column.label,
    cell: ({ row }) => {
      const value = row.original[column.key];

      if (column.type === "boolean-badge") {
        const active = Boolean(value);
        return h(resolveComponent("UBadge"), {
          color: active ? "success" : "warning",
          variant: "subtle"
        }, () => active ? (column.trueLabel || "active") : (column.falseLabel || "inactive"));
      }

      if (column.type === "datetime") {
        return toDateTime(value);
      }

      return toDisplayValue(value);
    }
  }));

  configuredColumns.push({
    id: "actions",
    header: "Actions",
    cell: ({ row }) => h("div", { class: "flex items-center justify-end gap-2" }, [
      h(resolveComponent("UButton"), {
        size: "xs",
        color: "neutral",
        variant: "soft",
        onClick: () => openDetails(row.original)
      }, () => "View"),
      h(resolveComponent("UButton"), {
        size: "xs",
        color: "neutral",
        variant: "soft",
        onClick: () => openEdit(row.original)
      }, () => "Edit"),
      h(resolveComponent("UButton"), {
        size: "xs",
        color: "error",
        variant: "soft",
        onClick: () => remove(row.original)
      }, () => "Delete")
    ])
  });

  return configuredColumns;
});

const detailFieldKeys = computed<string[]>(() => {
  if (!extension.value) {
    return [];
  }

  const keys = new Set<string>([extension.value.primaryKey]);
  extension.value.columns.forEach((column) => keys.add(column.key));
  extension.value.formFields.forEach((field) => {
    if (field.type !== "password") {
      keys.add(field.key);
    }
  });

  return Array.from(keys);
});

watch(routePath, async () => {
  await resolvePageExtension();
  await loadEntries();
});

await resolvePageExtension();
await loadEntries();
</script>

<template>
  <UDashboardPanel id="plugin-crud-page">
    <template #header>
      <UDashboardNavbar :title="extension?.title || 'Plugin Page'">
        <template #right>
          <UButton
            icon="i-lucide-plus"
            :disabled="!extension"
            @click="openCreate"
          >
            Add
          </UButton>
        </template>
      </UDashboardNavbar>
    </template>

    <template #body>
      <UAlert
        v-if="loadingExtension"
        color="info"
        variant="soft"
        description="Loading plugin extension..."
      />

      <UAlert
        v-else-if="!extension"
        color="error"
        variant="soft"
        title="Plugin page not found"
        description="No plugin registered this admin page route."
      />

      <template v-else>
        <UAlert
          v-if="extension.description"
          class="mb-4"
          color="info"
          variant="soft"
          :description="extension.description"
        />

        <UAlert
          v-if="listError"
          class="mb-4"
          color="error"
          variant="soft"
          :description="listError"
        />

        <UPageCard>
          <UTable
            :loading="loading"
            :columns="tableColumns"
            :data="entries"
            :empty="extension.emptyMessage || 'No entries available.'"
          />
        </UPageCard>
      </template>
    </template>
  </UDashboardPanel>

  <UModal
    v-model:open="detailsModalOpen"
    :title="extension?.title || 'Details'"
  >
    <template #body>
      <div
        v-if="selectedEntry && extension"
        class="space-y-2 text-sm"
      >
        <div
          v-for="key in detailFieldKeys"
          :key="key"
          class="flex items-start justify-between gap-4"
        >
          <span class="text-muted">{{ key }}</span>
          <span class="text-right">{{ toDisplayValue(selectedEntry[key]) }}</span>
        </div>
      </div>
    </template>
  </UModal>

  <UModal
    v-model:open="editModalOpen"
    :title="editTargetId ? `Edit ${extension?.title || 'Entry'}` : `Create ${extension?.title || 'Entry'}`"
  >
    <template #body>
      <div
        v-if="extension"
        class="space-y-4"
      >
        <UFormField
          v-for="field in extension.formFields"
          :key="field.key"
          :label="field.label"
          :required="isFieldRequired(field)"
        >
          <USwitch
            v-if="field.type === 'boolean'"
            :model-value="Boolean(formState[field.key])"
            @update:model-value="(value) => { formState[field.key] = value; }"
          />
          <UInput
            v-else
            :model-value="String(formState[field.key] ?? '')"
            :type="field.type === 'password' ? 'password' : 'text'"
            class="w-full"
            @update:model-value="(value) => { formState[field.key] = value; }"
          />
        </UFormField>

        <UAlert
          v-if="saveError"
          color="error"
          variant="soft"
          :description="saveError"
        />
      </div>
    </template>

    <template #footer>
      <div class="flex w-full items-center justify-end gap-2">
        <UButton
          color="neutral"
          variant="soft"
          @click="editModalOpen = false"
        >
          Cancel
        </UButton>
        <UButton
          :loading="saving"
          @click="save"
        >
          Save
        </UButton>
      </div>
    </template>
  </UModal>
</template>
