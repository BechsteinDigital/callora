import type { ShellPluginManifest, ShellPluginManifestEntry, ShellPluginStyleEntry } from "#shell-core/types/shell-extensions";

export interface PluginAssetLoaderOptions {
  /** Manifest surface to load, for example "admin" or "workspace". */
  surface: string;
  /** Unique state key so both shells can coexist in one Nuxt app tree. */
  stateKey: string;
}

const loadPromises = new Map<string, Promise<void>>();

function normalizeEntryPath(entryPath: string): string {
  const normalized = entryPath?.trim().replace(/\\/g, "/") ?? "";
  if (!normalized) {
    return "";
  }

  const marker = "custom/plugins/";
  const markerIndex = normalized.toLowerCase().indexOf(marker);
  if (markerIndex >= 0) {
    return normalized.slice(markerIndex + marker.length);
  }

  return normalized.replace(/^\/+/, "");
}

function isJavaScriptEntry(entry: ShellPluginManifestEntry, surface: string): boolean {
  if (!entry || entry.surface !== surface) {
    return false;
  }

  const entryPath = normalizeEntryPath(entry.entryPath);
  return entryPath.endsWith(".js") || entryPath.endsWith(".mjs");
}

function resolveScriptUrl(assetBase: string, entryPath: string): string {
  const relativePath = normalizeEntryPath(entryPath);
  if (!relativePath) {
    return "";
  }

  const trimmedBase = assetBase.trim().replace(/\/$/, "");
  if (!trimmedBase) {
    return `/plugin-assets/${relativePath}`;
  }

  return `${trimmedBase}/${relativePath}`;
}

function sortByChain<TEntry extends { pluginId: string }>(
  entries: TEntry[],
  orderedPluginIds: string[] | undefined
): TEntry[] {
  if (!orderedPluginIds || orderedPluginIds.length === 0) {
    return entries;
  }

  const rankByPluginId = new Map<string, number>();
  orderedPluginIds.forEach((pluginId, index) => {
    rankByPluginId.set(pluginId.trim().toLowerCase(), index);
  });

  return entries.slice().sort((left, right) => {
    const leftRank = rankByPluginId.get(left.pluginId.toLowerCase()) ?? Number.MAX_SAFE_INTEGER;
    const rightRank = rankByPluginId.get(right.pluginId.toLowerCase()) ?? Number.MAX_SAFE_INTEGER;
    return leftRank - rightRank;
  });
}

async function appendScript(url: string): Promise<void> {
  if (!url) {
    return;
  }

  const existing = document.querySelector(`script[data-callora-plugin-entry="${url}"]`);
  if (existing) {
    return;
  }

  await new Promise<void>((resolve, reject) => {
    const script = document.createElement("script");
    script.async = true;
    script.src = url;
    script.dataset.calloraPluginEntry = url;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error(`Failed to load plugin asset '${url}'.`));
    document.head.appendChild(script);
  });
}

function appendStylesheet(url: string): void {
  if (!url) {
    return;
  }

  const existing = document.querySelector(`link[data-callora-plugin-style="${url}"]`);
  if (existing) {
    return;
  }

  const link = document.createElement("link");
  link.rel = "stylesheet";
  link.href = url;
  link.dataset.calloraPluginStyle = url;
  document.head.appendChild(link);
}

/**
 * Shared manifest-driven plugin script loader. One broken plugin script does
 * not block the others; an absent manifest is tolerated.
 */
export function createPluginAssetLoader(options: PluginAssetLoaderOptions) {
  const runtimeConfig = useRuntimeConfig();
  const isLoaded = useState<boolean>(options.stateKey, () => false);

  function resolveManifestUrl(): string {
    const configuredPath = runtimeConfig.public.calloraPluginManifestPath || "/manifests/plugin-ui-assets.manifest.json";
    if (configuredPath.startsWith("http://") || configuredPath.startsWith("https://")) {
      return configuredPath;
    }

    return configuredPath.startsWith("/") ? configuredPath : `/${configuredPath}`;
  }

  function resolveAssetBase(): string {
    const configuredBase = runtimeConfig.public.calloraPluginAssetsBase ?? "";
    if (configuredBase.trim().length > 0) {
      return configuredBase;
    }

    return runtimeConfig.public.calloraApiBase ?? "";
  }

  async function ensureLoaded(orderedPluginIds?: string[]): Promise<void> {
    if (isLoaded.value) {
      return;
    }

    const pending = loadPromises.get(options.stateKey);
    if (pending) {
      await pending;
      return;
    }

    const loadPromise = (async () => {
      try {
        const manifestUrl = resolveManifestUrl();
        const manifest = await $fetch<ShellPluginManifest>(manifestUrl, {
          baseURL: manifestUrl.startsWith("http://") || manifestUrl.startsWith("https://")
            ? undefined
            : resolveAssetBase() || undefined
        });

        // Stylesheets append in template-chain order before any script runs,
        // so theme CSS can override base styles by cascade order.
        const styleEntries = sortByChain(
          (manifest.styleEntries ?? []).filter(
            (entry: ShellPluginStyleEntry) => entry.surface === options.surface
          ),
          orderedPluginIds
        );
        for (const styleEntry of styleEntries) {
          appendStylesheet(resolveScriptUrl(resolveAssetBase(), styleEntry.stylePath));
        }

        // Scripts load sequentially in template-chain order so later bundles
        // can extend blocks contributed by earlier ones.
        const entries = sortByChain(
          manifest.entries.filter((entry) => isJavaScriptEntry(entry, options.surface)),
          orderedPluginIds
        );
        for (const entry of entries) {
          const scriptUrl = resolveScriptUrl(resolveAssetBase(), entry.entryPath);
          if (!scriptUrl) {
            continue;
          }

          try {
            await appendScript(scriptUrl);
          } catch {
            // Ein defektes Plugin-Skript darf die übrigen nicht blockieren.
          }
        }
      } catch {
        // Manifest kann in frühen Dev-Zuständen fehlen.
      } finally {
        isLoaded.value = true;
      }
    })();

    loadPromises.set(options.stateKey, loadPromise);
    await loadPromise;
  }

  return {
    ensureLoaded
  };
}
