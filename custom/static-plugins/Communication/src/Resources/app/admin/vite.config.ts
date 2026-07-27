import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// Builds the plugin's admin UI as a single IIFE the host shell loads at runtime.
// Vue is EXTERNAL and mapped to the host's shared instance (window.CalloraAdmin.vue)
// — the plugin never bundles its own Vue runtime (two runtimes break reactivity and
// component instancing across the boundary). Output lands in ../public/admin, the
// directory the host's PluginUiAssetPublisher publishes for the "admin" surface.
export default defineConfig({
  plugins: [vue()],
  define: { 'process.env.NODE_ENV': '"production"' },
  build: {
    outDir: fileURLToPath(new URL('../../public/admin', import.meta.url)),
    emptyOutDir: true,
    cssCodeSplit: false,
    lib: {
      entry: fileURLToPath(new URL('./src/main.ts', import.meta.url)),
      formats: ['iife'],
      name: 'CalloraCommunicationAdminUi',
      fileName: () => 'main.js',
    },
    rollupOptions: {
      external: ['vue'],
      output: { globals: { vue: 'CalloraAdmin.vue' } },
    },
  },
})
