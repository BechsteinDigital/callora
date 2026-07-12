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

export type WorkspaceBlockExtension = import("#shell-core/types/shell-extensions").ShellBlockExtension;

export type WorkspaceFragment = import("#shell-core/types/shell-extensions").ShellFragment;

export type WorkspaceSnippetRegistration = {
  locale: string;
  values: Record<string, string>;
};

export type WorkspaceUiBridge = {
  context?: WorkspaceBridgeContext;
  getContext?: () => WorkspaceBridgeContext;
  registerInfoBanner?: (banner: WorkspaceInfoBanner) => void;
  queuedInfoBanners?: WorkspaceInfoBanner[];
  registerWidget?: (widget: WorkspaceWidget) => void;
  queuedWidgets?: WorkspaceWidget[];
  registerBlockExtension?: (extension: WorkspaceBlockExtension) => void;
  queuedBlockExtensions?: WorkspaceBlockExtension[];
  registerFragment?: (fragment: WorkspaceFragment) => void;
  queuedFragments?: WorkspaceFragment[];
  registerSnippets?: (locale: string, values: Record<string, string>) => void;
  queuedSnippets?: WorkspaceSnippetRegistration[];
  translate?: (snippetKey: string, fallback?: string) => string;
  mountFragment?: (fragmentName: string, container: HTMLElement) => (() => void) | void;
  listBlocks?: () => string[];
};
