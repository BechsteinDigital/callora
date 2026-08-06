import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

/**
 * The LIBRARY build of this project, next to the application build in `vite.config.ts`.
 *
 * The same sources serve two purposes: the shell that Callora ships (an app, bundled into
 * wwwroot/admin) and `@callora/admin`, the contract plugin authors compile against (a library in
 * dist-lib/). Umbraco does the same with `vite.cms.config.ts` beside `vite.config.ts` — one
 * project, two outputs, no second copy of the components to keep in step.
 *
 * Only `src/public/*` is built: that directory IS the contract. Everything else is the shell's
 * own business, reachable through the alias but not through a package entry point.
 *
 * Everything a consumer already has stays external — Vue, the router, the component libraries.
 * Bundling them would give a plugin a second Vue, which breaks reactivity across the boundary.
 */
export default defineConfig({
  plugins: [vue()],
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  css: { preprocessorOptions: { scss: { api: 'modern-compiler' } } },
  build: {
    outDir: 'dist-lib',
    emptyOutDir: true,
    // Consumers import subpaths (`@callora/admin/components`), so each entry keeps its own file
    // rather than everything collapsing into one bundle.
    lib: {
      entry: {
        'public/index': fileURLToPath(new URL('./src/public/index.ts', import.meta.url)),
        'public/extensions': fileURLToPath(new URL('./src/public/extensions.ts', import.meta.url)),
        'public/components': fileURLToPath(new URL('./src/public/components.ts', import.meta.url)),
        'public/tokens': fileURLToPath(new URL('./src/public/tokens.ts', import.meta.url)),
        'public/patterns': fileURLToPath(new URL('./src/public/patterns.ts', import.meta.url)),
        'vite-preset': fileURLToPath(new URL('./src/public/vite-preset.ts', import.meta.url)),
      },
      formats: ['es'],
    },
    rollupOptions: {
      external: ['vue', 'vue-router', 'radix-vue', 'lucide-vue-next', 'vite', '@vitejs/plugin-vue'],
      output: { entryFileNames: '[name].js', chunkFileNames: 'chunks/[name]-[hash].js' },
    },
  },
})
