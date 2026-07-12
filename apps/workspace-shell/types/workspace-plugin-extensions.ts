export type WorkspacePluginManifestEntry = import("#shell-core/types/shell-extensions").ShellPluginManifestEntry;

export type WorkspacePluginTemplateEntry = import("#shell-core/types/shell-extensions").ShellPluginTemplateEntry;

export type WorkspacePluginManifest = import("#shell-core/types/shell-extensions").ShellPluginManifest;

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

export type WorkspaceWidgetOverrideMode = import("#shell-core/types/shell-extensions").ShellWidgetOverrideMode;

export type WorkspaceWidgetOverride = import("#shell-core/types/shell-extensions").ShellWidgetOverride;

export type WorkspaceWidget = import("#shell-core/types/shell-extensions").ShellWidget<WorkspaceWidgetSlot>;

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
