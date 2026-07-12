import type { ShellBlockMountContext } from "#shell-core/types/shell-extensions";

export interface ShellPluginPage {
  /** Route path without workspace prefix, e.g. "/calls". */
  path: string;
  pluginId: string;
  /** Navigation label shown to users. */
  title: string;
  icon?: string;
  navOrder?: number;
  /**
   * Renders the page into a live container. The host page wraps the content
   * with `workspace.<pageId>.before/after` blocks so other plugins can extend
   * plugin pages the same way as shell pages. May return a cleanup function.
   */
  mount?: (container: HTMLElement, context: ShellBlockMountContext) => (() => void) | void;
  /** Vue component alternative to mount — rendered in the shell's Vue instance. */
  component?: unknown;
}

export interface ShellPluginPageEntry extends ShellPluginPage {
  pageId: string;
}

function toPageId(path: string): string {
  return path.replace(/^\/+|\/+$/g, "").replace(/\//g, "-") || "index";
}

/**
 * Registry for full pages contributed by plugin bundles — the shell adds a
 * route and a navigation entry per page.
 */
export function createShellPageRegistry(stateKey: string) {
  const pages = useState<ShellPluginPageEntry[]>(stateKey, () => []);

  function registerPage(input: ShellPluginPage): ShellPluginPageEntry | null {
    const path = input.path?.trim();
    const pluginId = input.pluginId?.trim();
    const title = input.title?.trim();
    if (!path || !path.startsWith("/") || !pluginId || !title || (!input.mount && !input.component)) {
      return null;
    }

    const entry: ShellPluginPageEntry = {
      ...input,
      path,
      pluginId,
      title,
      pageId: toPageId(path)
    };

    pages.value = [
      ...pages.value.filter((existing) => existing.path !== path),
      entry
    ];
    return entry;
  }

  function listPages(): ShellPluginPageEntry[] {
    return [...pages.value].sort((left, right) =>
      (left.navOrder ?? 100) - (right.navOrder ?? 100) || left.title.localeCompare(right.title));
  }

  function findPage(path: string): ShellPluginPageEntry | undefined {
    return pages.value.find((entry) => entry.path === path);
  }

  return {
    pages: readonly(pages),
    registerPage,
    listPages,
    findPage
  };
}
