export function useAdminPluginAssets() {
  const loader = createPluginAssetLoader({
    surface: "admin",
    stateKey: "admin-plugin-assets-loaded"
  });

  return {
    ensureAdminPluginAssetsLoaded: loader.ensureLoaded
  };
}
