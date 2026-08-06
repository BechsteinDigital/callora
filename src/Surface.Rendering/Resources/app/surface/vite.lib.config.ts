import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

/**
 * The LIBRARY build, next to the application build in `vite.config.ts`.
 *
 * The same sources serve two purposes: the runtime Callora ships (an app, bundled into
 * wwwroot/surface-app) and `@callora/surface`, the contract plugin authors compile
 * against (a library in dist-lib/). Mirrors the admin shell, which mirrors Umbraco —
 * one project, two outputs, no second copy of the contract to keep in step.
 *
 * Only `src/public/*` is built: that directory IS the contract.
 *
 * Everything a consumer already has stays external. Vue especially: bundling it would
 * give a plugin a second Vue instance, and reactivity does not cross that boundary —
 * the very failure the runtime's window.CalloraVue global exists to prevent.
 */
export default defineConfig({
  plugins: [vue()],
  build: {
    outDir: 'dist-lib',
    emptyOutDir: true,
    lib: {
      entry: {
        'public/index': fileURLToPath(new URL('./src/public/index.ts', import.meta.url)),
        'public/context': fileURLToPath(new URL('./src/public/context.ts', import.meta.url)),
        'vite-preset': fileURLToPath(new URL('./src/public/vite-preset.ts', import.meta.url)),
      },
      formats: ['es'],
    },
    rollupOptions: {
      external: ['vue', 'vite', '@vitejs/plugin-vue'],
      output: { entryFileNames: '[name].js', chunkFileNames: 'chunks/[name]-[hash].js' },
    },
  },
})
