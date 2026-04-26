export type WorkspacePluginManifestEntry = {
  pluginId: string;
  surface: string;
  entryPath: string;
};

export type WorkspacePluginTemplateEntry = {
  pluginId: string;
  templatePath: string;
};

export type WorkspacePluginManifest = {
  generatedAtUtc: string;
  entries: WorkspacePluginManifestEntry[];
  workspaceTemplates: WorkspacePluginTemplateEntry[];
};

export type WorkspaceInfoBanner = {
  id: string;
  pluginId?: string;
  title: string;
  description?: string;
};

export type WorkspaceWidgetSlot =
  | "dashboard.main"
  | "content.main"
  | "sidebar.main";

export type WorkspaceWidgetOverrideMode =
  | "replace"
  | "before"
  | "after";

export type WorkspaceWidgetOverride = {
  targetWidgetKey: string;
  mode: WorkspaceWidgetOverrideMode;
};

export type WorkspaceWidget = {
  widgetKey: string;
  pluginId: string;
  slot: WorkspaceWidgetSlot;
  title: string;
  description?: string;
  contentHtml?: string;
  order?: number;
  priority?: number;
  override?: WorkspaceWidgetOverride;
};

export type WorkspaceBridgeContext = {
  workspace: {
    key: string;
    name: string;
    type: string;
  };
  route: {
    publicBaseUrl: string;
    publicPathPrefix: string;
  };
};

export type WorkspaceUiBridge = {
  context?: WorkspaceBridgeContext;
  getContext?: () => WorkspaceBridgeContext;
  registerInfoBanner?: (banner: WorkspaceInfoBanner) => void;
  queuedInfoBanners?: WorkspaceInfoBanner[];
  registerWidget?: (widget: WorkspaceWidget) => void;
  queuedWidgets?: WorkspaceWidget[];
};
