export function useWorkspacePluginAssets() {
  const loader = createPluginAssetLoader({
    surface: "workspace",
    stateKey: "workspace-plugin-assets-loaded"
  });

  return {
    ensureWorkspacePluginAssetsLoaded: loader.ensureLoaded
  };
}
