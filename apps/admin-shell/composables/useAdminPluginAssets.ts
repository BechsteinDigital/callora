import type { PluginAdminUiManifest, PluginAdminUiManifestEntry } from "~/types/admin-plugin-extensions";

let loadPromise: Promise<void> | null = null;

function normalizeEntryPath(entryPath: string): string {
  const normalized = entryPath.trim().replace(/\\/g, "/");
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

function isAdminJavaScriptEntry(entry: PluginAdminUiManifestEntry): boolean {
  if (!entry || entry.surface !== "admin") {
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

export function useAdminPluginAssets() {
  const runtimeConfig = useRuntimeConfig();
  const isLoaded = useState<boolean>("admin-plugin-assets-loaded", () => false);

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

  function resolveManifestBase(): string {
    const configuredBase = runtimeConfig.public.calloraPluginAssetsBase ?? "";
    if (configuredBase.trim().length > 0) {
      return configuredBase;
    }

    return runtimeConfig.public.calloraApiBase ?? "";
  }

  async function ensureAdminPluginAssetsLoaded(): Promise<void> {
    if (isLoaded.value) {
      return;
    }

    if (loadPromise) {
      await loadPromise;
      return;
    }

    loadPromise = (async () => {
      try {
        const manifestUrl = resolveManifestUrl();
        const manifest = await $fetch<PluginAdminUiManifest>(manifestUrl, {
          baseURL: manifestUrl.startsWith("http://") || manifestUrl.startsWith("https://")
            ? undefined
            : resolveManifestBase() || undefined
        });

        const entries = manifest.entries.filter(isAdminJavaScriptEntry);
        for (const entry of entries) {
          const scriptUrl = resolveScriptUrl(resolveAssetBase(), entry.entryPath);
          if (!scriptUrl) {
            continue;
          }

          try {
            await appendScript(scriptUrl);
          } catch {
            // Skip failing plugin assets so one broken plugin does not block all others.
          }
        }
      } catch {
        // Manifest can be absent in some development states.
      } finally {
        isLoaded.value = true;
      }
    })();

    await loadPromise;
  }

  return {
    ensureAdminPluginAssetsLoaded
  };
}
