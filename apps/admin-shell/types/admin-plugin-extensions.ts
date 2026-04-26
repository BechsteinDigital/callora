export interface PluginAdminNavigationItem {
  pluginId: string;
  id: string;
  label: string;
  to: string;
  icon: string | null;
  order: number;
}

export type AdminPluginCrudColumnType = 'text' | 'boolean-badge' | 'datetime';

export interface AdminPluginCrudColumn {
  key: string;
  label: string;
  type?: AdminPluginCrudColumnType;
  trueLabel?: string;
  falseLabel?: string;
}

export type AdminPluginCrudFieldType = 'text' | 'password' | 'boolean';

export interface AdminPluginCrudField {
  key: string;
  label: string;
  type?: AdminPluginCrudFieldType;
  required?: boolean;
  requiredOnCreateOnly?: boolean;
}

export interface AdminPluginCrudPageExtension {
  id: string;
  pluginId: string;
  title: string;
  routePath: string;
  apiBasePath: string;
  primaryKey: string;
  icon?: string;
  description?: string;
  emptyMessage?: string;
  columns: AdminPluginCrudColumn[];
  formFields: AdminPluginCrudField[];
}

export type AdminWidgetSlot =
  | 'dashboard.main'
  | 'plugins.main'
  | 'sidebar.main';

export type AdminWidgetOverrideMode =
  | 'replace'
  | 'before'
  | 'after';

export interface AdminWidgetOverride {
  targetWidgetKey: string;
  mode: AdminWidgetOverrideMode;
}

export interface AdminWidget {
  widgetKey: string;
  pluginId: string;
  slot: AdminWidgetSlot;
  title: string;
  description?: string;
  contentHtml?: string;
  order?: number;
  priority?: number;
  override?: AdminWidgetOverride;
}

export interface PluginAdminUiManifestEntry {
  pluginId: string;
  surface: string;
  entryPath: string;
}

export interface PluginAdminUiManifest {
  generatedAtUtc: string;
  entries: PluginAdminUiManifestEntry[];
}

export interface AdminUiBridge {
  registerLoginNoticeExtension?: (extension: import("~/types/admin-login-extensions").AdminLoginNoticeExtension) => void;
  registerPageExtension?: (extension: AdminPluginCrudPageExtension) => void;
  registerWidget?: (widget: AdminWidget) => void;
  queuedLoginNoticeExtensions?: import("~/types/admin-login-extensions").AdminLoginNoticeExtension[];
  queuedPageExtensions?: AdminPluginCrudPageExtension[];
  queuedWidgets?: AdminWidget[];
}
