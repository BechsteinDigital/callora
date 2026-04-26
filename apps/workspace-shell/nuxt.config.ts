export default defineNuxtConfig({
  modules: ["@nuxt/ui", "@pinia/nuxt"],
  ssr: false,
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
    baseURL: import.meta.env.NUXT_PUBLIC_WORKSPACE_BASE_URL ?? "/",
    head: {
      script: [
        {
          src: "/workspace/public/bootstrap.js"
        }
      ]
    }
  },
  runtimeConfig: {
    public: {
      calloraApiBase: import.meta.env.NUXT_PUBLIC_CALLORA_API_BASE ?? "",
      calloraPluginAssetsBase: import.meta.env.NUXT_PUBLIC_CALLORA_PLUGIN_ASSETS_BASE ?? "",
      calloraPluginManifestPath: import.meta.env.NUXT_PUBLIC_CALLORA_PLUGIN_MANIFEST_PATH ?? "/manifests/plugin-ui-assets.manifest.json",
      workspaceDisplayName: import.meta.env.NUXT_PUBLIC_WORKSPACE_DISPLAY_NAME ?? "Callora Workspace",
      workspaceType: import.meta.env.NUXT_PUBLIC_WORKSPACE_TYPE ?? "base",
      workspaceDashboardPath: import.meta.env.NUXT_PUBLIC_WORKSPACE_DASHBOARD_PATH ?? "/dashboard"
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
  compatibilityDate: "2026-04-22"
});
