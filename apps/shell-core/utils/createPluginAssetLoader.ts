import type { ShellPluginManifest, ShellPluginManifestEntry } from "#shell-core/types/shell-extensions";

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

  async function ensureLoaded(): Promise<void> {
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

        const entries = manifest.entries.filter((entry) => isJavaScriptEntry(entry, options.surface));
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
