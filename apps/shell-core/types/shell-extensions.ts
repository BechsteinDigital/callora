export interface ShellPluginManifestEntry {
  pluginId: string;
  surface: string;
  entryPath: string;
}

export interface ShellPluginTemplateEntry {
  pluginId: string;
  templatePath: string;
}

export interface ShellPluginStyleEntry {
  pluginId: string;
  surface: string;
  stylePath: string;
}

export interface ShellPluginManifest {
  generatedAtUtc: string;
  entries: ShellPluginManifestEntry[];
  styleEntries?: ShellPluginStyleEntry[];
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

export type ShellBlockMode = "append" | "prepend" | "replace";

export interface ShellBlockMountContext {
  blockName: string;
  workspaceKey: string;
  /** Resolves a registered snippet for the active locale — Twig's trans. */
  translate?: (snippetKey: string, fallback?: string) => string;
  /** Renders a registered fragment into a container — Twig's include. */
  mountFragment?: (fragmentName: string, container: HTMLElement) => (() => void) | void;
  [key: string]: unknown;
}

export interface ShellFragment {
  fragmentName: string;
  pluginId: string;
  contentHtml?: string;
  mount?: (container: HTMLElement) => (() => void) | void;
}

export interface ShellBlockExtension {
  blockName: string;
  pluginId: string;
  /** How the extension composes with the block default. Default: "append". */
  mode?: ShellBlockMode;
  /**
   * Static HTML content rendered inside the block. A "replace" extension can
   * embed the replaced content — Twig's parent() — by placing a
   * `<div data-shell-parent></div>` marker; the next lower chain level (or
   * the block default) renders into it. The marker must exist synchronously
   * in contentHtml or right after mount() returns.
   */
  contentHtml?: string;
  /**
   * Interactive content: called with a live container element once the block
   * renders. May return a cleanup function invoked on unmount.
   */
  mount?: (container: HTMLElement, context: ShellBlockMountContext) => (() => void) | void;
  /** Higher priority wins for "replace" and orders extensions within a mode. */
  priority?: number;
}

export interface ShellBlockExtensionEntry extends ShellBlockExtension {
  mode: ShellBlockMode;
  priority: number;
  registrationOrder: number;
}
