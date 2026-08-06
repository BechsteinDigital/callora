import type { UserConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export interface AdminPluginPresetOptions {
  /** Entry module of the plugin's admin UI, e.g. 'src/Resources/app/admin/src/main.ts'. */
  entry: string
  /** Global name for the IIFE bundle. Must be unique per plugin. */
  name: string
  /** Output directory. Default 'src/Resources/public/admin' — only this is published. */
  outDir?: string
}

/**
 * The blessed Vite config for a Callora admin plugin.
 *
 * Source lives under `app/`, the compiled deliverable under `Resources/public` — only the
 * deliverable ships, the same split the surface side uses. The result is ONE self-registering
 * IIFE bundle with fixed names (`main.js`, `main.css`), because the host's asset manifest and
 * loader address it by exactly those.
 *
 * Vue stays EXTERNAL and resolves from the shell's global. A plugin that bundled its own Vue
 * would run a second runtime: reactivity and component instancing break across that boundary, in
 * ways that surface as "my component renders but never updates".
 *
 * Before this preset existed, each plugin copied twenty-eight lines of configuration and had to
 * keep them in step with the host by hand.
 */
export function calloraAdminPlugin(options: AdminPluginPresetOptions): UserConfig {
  return {
    plugins: [vue()],
    // A plugin bundle is built once and served as-is; there is no environment to read at
    // runtime, and Vue's browser build branches on this value.
    define: { 'process.env.NODE_ENV': '"production"' },
    build: {
      outDir: options.outDir ?? 'src/Resources/public/admin',
      emptyOutDir: true,
      cssCodeSplit: false,
      lib: {
        entry: options.entry,
        formats: ['iife'],
        name: options.name,
        fileName: () => 'main.js',
      },
      rollupOptions: {
        external: ['vue'],
        output: {
          globals: { vue: 'CalloraVue' },
          assetFileNames: (asset) =>
            asset.names.some((name) => name.endsWith('.css'))
              ? 'main.css'
              : (asset.names[0] ?? '[name][extname]'),
        },
      },
    },
  }
}
