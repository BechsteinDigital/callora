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

export type AdminWidgetOverrideMode = import("#shell-core/types/shell-extensions").ShellWidgetOverrideMode;

export type AdminWidgetOverride = import("#shell-core/types/shell-extensions").ShellWidgetOverride;

export type AdminWidget = import("#shell-core/types/shell-extensions").ShellWidget<AdminWidgetSlot>;

export type PluginAdminUiManifestEntry = import("#shell-core/types/shell-extensions").ShellPluginManifestEntry;

export type PluginAdminUiManifest = import("#shell-core/types/shell-extensions").ShellPluginManifest;

export type AdminBlockExtension = import("#shell-core/types/shell-extensions").ShellBlockExtension;

export type AdminFragment = import("#shell-core/types/shell-extensions").ShellFragment;

export type AdminSnippetRegistration = {
  locale: string;
  values: Record<string, string>;
};

export interface AdminUiBridge {
  registerLoginNoticeExtension?: (extension: import("~/types/admin-login-extensions").AdminLoginNoticeExtension) => void;
  registerPageExtension?: (extension: AdminPluginCrudPageExtension) => void;
  registerWidget?: (widget: AdminWidget) => void;
  queuedLoginNoticeExtensions?: import("~/types/admin-login-extensions").AdminLoginNoticeExtension[];
  queuedPageExtensions?: AdminPluginCrudPageExtension[];
  queuedWidgets?: AdminWidget[];
  registerBlockExtension?: (extension: AdminBlockExtension) => void;
  queuedBlockExtensions?: AdminBlockExtension[];
  registerFragment?: (fragment: AdminFragment) => void;
  queuedFragments?: AdminFragment[];
  registerSnippets?: (locale: string, values: Record<string, string>) => void;
  queuedSnippets?: AdminSnippetRegistration[];
  translate?: (snippetKey: string, fallback?: string) => string;
  mountFragment?: (fragmentName: string, container: HTMLElement) => (() => void) | void;
  listBlocks?: () => string[];
}
