import type { WorkspacePluginManifest, WorkspacePluginManifestEntry } from "~/types/workspace-plugin-extensions";

let loadPromise: Promise<void> | null = null;

function isWorkspaceJavaScriptEntry(entry: WorkspacePluginManifestEntry): boolean {
  if (!entry || entry.surface !== "workspace") {
    return false;
  }

  const path = entry.entryPath?.trim().toLowerCase();
  return Boolean(path?.endsWith(".js") || path?.endsWith(".mjs"));
}

function resolveScriptUrl(assetBase: string, entryPath: string): string {
  const normalizedPath = entryPath.trim().replace(/\\/g, "/").replace(/^\/+/, "");
  if (!normalizedPath) {
    return "";
  }

  const trimmedBase = assetBase.trim().replace(/\/$/, "");
  if (!trimmedBase) {
    return `/plugin-assets/${normalizedPath}`;
  }

  return `${trimmedBase}/${normalizedPath}`;
}

async function appendScript(url: string): Promise<void> {
  if (!url) {
    return;
  }

  const existing = document.querySelector(`script[data-callora-workspace-plugin-entry="${url}"]`);
  if (existing) {
    return;
  }

  await new Promise<void>((resolve, reject) => {
    const script = document.createElement("script");
    script.async = true;
    script.src = url;
    script.dataset.calloraWorkspacePluginEntry = url;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error(`Failed to load workspace plugin asset '${url}'.`));
    document.head.appendChild(script);
  });
}

export function useWorkspacePluginAssets() {
  const runtimeConfig = useRuntimeConfig();
  const isLoaded = useState<boolean>("workspace-plugin-assets-loaded", () => false);

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

  async function ensureWorkspacePluginAssetsLoaded(): Promise<void> {
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
        const manifest = await $fetch<WorkspacePluginManifest>(manifestUrl, {
          baseURL: manifestUrl.startsWith("http://") || manifestUrl.startsWith("https://")
            ? undefined
            : resolveManifestBase() || undefined
        });

        const entries = manifest.entries.filter(isWorkspaceJavaScriptEntry);
        for (const entry of entries) {
          const scriptUrl = resolveScriptUrl(resolveAssetBase(), entry.entryPath);
          if (!scriptUrl) {
            continue;
          }

          try {
            await appendScript(scriptUrl);
          } catch {
            // One broken plugin script should not block all workspace plugins.
          }
        }
      } catch {
        // Manifest can be absent in early bootstrap or isolated local runs.
      } finally {
        isLoaded.value = true;
      }
    })();

    await loadPromise;
  }

  return {
    ensureWorkspacePluginAssetsLoaded
  };
}
