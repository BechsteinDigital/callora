import type { AdminPluginCrudPageExtension } from "~/types/admin-plugin-extensions";

function normalizeRoutePath(path: string): string {
  const trimmed = path.trim();
  if (!trimmed) {
    return "";
  }

  return trimmed.startsWith("/") ? trimmed : `/${trimmed}`;
}

function sanitizeCrudPageExtension(extension: AdminPluginCrudPageExtension): AdminPluginCrudPageExtension | null {
  const id = extension.id?.trim();
  const pluginId = extension.pluginId?.trim();
  const title = extension.title?.trim();
  const routePath = normalizeRoutePath(extension.routePath ?? "");
  const apiBasePath = normalizeRoutePath(extension.apiBasePath ?? "");
  const primaryKey = extension.primaryKey?.trim();

  if (!id || !pluginId || !title || !routePath || !apiBasePath || !primaryKey) {
    return null;
  }

  if (!Array.isArray(extension.columns) || extension.columns.length === 0) {
    return null;
  }

  if (!Array.isArray(extension.formFields) || extension.formFields.length === 0) {
    return null;
  }

  const columns = extension.columns
    .filter((column) => column?.key?.trim() && column?.label?.trim())
    .map((column) => ({
      key: column.key.trim(),
      label: column.label.trim(),
      type: column.type ?? "text",
      trueLabel: column.trueLabel?.trim(),
      falseLabel: column.falseLabel?.trim()
    }));

  const formFields = extension.formFields
    .filter((field) => field?.key?.trim() && field?.label?.trim())
    .map((field) => ({
      key: field.key.trim(),
      label: field.label.trim(),
      type: field.type ?? "text",
      required: field.required ?? false,
      requiredOnCreateOnly: field.requiredOnCreateOnly ?? false
    }));

  if (columns.length === 0 || formFields.length === 0) {
    return null;
  }

  return {
    id,
    pluginId,
    title,
    routePath,
    apiBasePath,
    primaryKey,
    icon: extension.icon?.trim(),
    description: extension.description?.trim(),
    emptyMessage: extension.emptyMessage?.trim(),
    columns,
    formFields
  };
}

export function useAdminPageExtensions() {
  const crudPageExtensions = useState<AdminPluginCrudPageExtension[]>("admin-crud-page-extensions", () => []);

  function registerCrudPageExtension(extension: AdminPluginCrudPageExtension): void {
    const sanitized = sanitizeCrudPageExtension(extension);
    if (!sanitized) {
      return;
    }

    const filtered = crudPageExtensions.value
      .filter((entry) => !(entry.pluginId === sanitized.pluginId && entry.id === sanitized.id));
    filtered.push(sanitized);
    crudPageExtensions.value = filtered;
  }

  function findCrudPageByRoute(routePath: string): AdminPluginCrudPageExtension | null {
    const normalized = normalizeRoutePath(routePath);
    return crudPageExtensions.value.find((entry) => entry.routePath === normalized) ?? null;
  }

  return {
    crudPageExtensions: readonly(crudPageExtensions),
    registerCrudPageExtension,
    findCrudPageByRoute
  };
}
