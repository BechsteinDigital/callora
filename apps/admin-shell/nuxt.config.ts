export default defineNuxtConfig({
  ssr: false,
  modules: ["@nuxt/ui", "@pinia/nuxt"],
  fonts: {
    provider: "local",
    providers: {
      adobe: false,
      google: false,
      googleicons: false,
      bunny: false,
      fontshare: false,
      fontsource: false
    }
  },
  css: ["~/assets/css/main.css"],
  app: {
    baseURL: "/admin/"
  },
  runtimeConfig: {
    public: {
      calloraApiBase: import.meta.env.NUXT_PUBLIC_CALLORA_API_BASE ?? "",
      calloraPluginAssetsBase: import.meta.env.NUXT_PUBLIC_CALLORA_PLUGIN_ASSETS_BASE ?? "",
      calloraPluginManifestPath: import.meta.env.NUXT_PUBLIC_CALLORA_PLUGIN_MANIFEST_PATH ?? "/manifests/plugin-ui-assets.manifest.json",
      defaultTenantKey: import.meta.env.NUXT_PUBLIC_DEFAULT_TENANT_KEY ?? "default",
      enableTenantManagementApi: import.meta.env.NUXT_PUBLIC_ENABLE_TENANT_MANAGEMENT_API === "true",
      enableWorkspaceManagementApi: import.meta.env.NUXT_PUBLIC_ENABLE_WORKSPACE_MANAGEMENT_API === "true",
      enableThemeManagementApi: import.meta.env.NUXT_PUBLIC_ENABLE_THEME_MANAGEMENT_API === "true"
    }
  },
  experimental: {
    serverAppConfig: false
  },
  devtools: {
    enabled: true
  },
  typescript: {
    strict: true,
    typeCheck: false
  },
  compatibilityDate: "2026-04-21"
});
