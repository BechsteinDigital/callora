export function useWorkspacePluginAssets() {
  const loader = createPluginAssetLoader({
    surface: "workspace",
    stateKey: "workspace-plugin-assets-loaded"
  });
  const runtimeConfig = useRuntimeConfig();
  const { workspaceKey } = useWorkspaceContext();

  async function fetchUiChain(): Promise<string[] | undefined> {
    try {
      const response = await $fetch<{ chain: string[] }>(
        `/workspace/public/ui-chain?workspaceKey=${encodeURIComponent(workspaceKey.value || "default")}`,
        {
          baseURL: runtimeConfig.public.calloraApiBase || undefined,
          credentials: "include"
        }
      );
      return response.chain;
    } catch {
      // Without a chain the loader falls back to manifest order.
      return undefined;
    }
  }

  async function ensureWorkspacePluginAssetsLoaded(): Promise<void> {
    const chain = await fetchUiChain();
    await loader.ensureLoaded(chain);
  }

  return {
    ensureWorkspacePluginAssetsLoaded
  };
}
