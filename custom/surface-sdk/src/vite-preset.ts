import type { UserConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export interface SurfacePluginPresetOptions {
  /** Entry module of the plugin's surface UI (e.g. 'src/Resources/app/workspace/src/main.ts'). */
  entry: string
  /** Global name for the IIFE bundle (must be unique per plugin). */
  name: string
  /** Surface the bundle targets; also the default output-dir segment. Default 'workspace'. */
  surface?: string
  /** Output directory. Default 'src/Resources/public/<surface>' (only this is published). */
  outDir?: string
}

/**
 * The blessed Vite config for a Callora surface plugin (Shopware-analog: source under
 * app/, compiled deliverable under Resources/public — only the deliverable ships). It
 * builds ONE self-registering IIFE bundle (main.js + main.css, fixed names) with Vue
 * kept EXTERNAL and resolved from the runtime's window.CalloraVue global, so the plugin
 * runs inside the surface runtime's single Vue instance instead of shipping its own.
 */
export function calloraSurfacePlugin(options: SurfacePluginPresetOptions): UserConfig {
  const surface = options.surface ?? 'workspace'
  const outDir = options.outDir ?? `src/Resources/public/${surface}`

  return {
    plugins: [vue()],
    build: {
      outDir,
      emptyOutDir: true,
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
            asset.names.some((assetName) => assetName.endsWith('.css'))
              ? 'main.css'
              : (asset.names[0] ?? '[name][extname]'),
        },
      },
    },
  }
}
