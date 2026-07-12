export interface ShellPluginManifestEntry {
  pluginId: string;
  surface: string;
  entryPath: string;
}

export interface ShellPluginTemplateEntry {
  pluginId: string;
  templatePath: string;
}

export interface ShellPluginManifest {
  generatedAtUtc: string;
  entries: ShellPluginManifestEntry[];
  workspaceTemplates?: ShellPluginTemplateEntry[];
}

export type ShellWidgetOverrideMode = "replace" | "before" | "after";

export interface ShellWidgetOverride {
  targetWidgetKey: string;
  mode: ShellWidgetOverrideMode;
}

export interface ShellWidget<TSlot extends string = string> {
  widgetKey: string;
  pluginId: string;
  slot: TSlot;
  title: string;
  description?: string;
  contentHtml?: string;
  order?: number;
  priority?: number;
  override?: ShellWidgetOverride;
}

export interface ShellWidgetEntry<TSlot extends string = string> extends ShellWidget<TSlot> {
  order: number;
  priority: number;
  registrationOrder: number;
}
