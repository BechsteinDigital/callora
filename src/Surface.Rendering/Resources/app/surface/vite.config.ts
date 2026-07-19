import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

// The surface runtime is the neutral grundgerüst that the SSR SurfaceShell loads
// into #callora-app (ADR-014 §8). It builds as ONE self-mounting IIFE bundle with
// fixed names (surface.js / surface.css) — no index.html of its own; the server-
// rendered shell is the document. Vue is BUNDLED here (the runtime owns the single
// instance and re-exposes it as window.CalloraVue); plugin bundles keep Vue external
// and resolve it from that global, so everything shares one Vue.
export default defineConfig({
  plugins: [vue()],
  base: '/surface-app/',
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  build: {
    outDir: fileURLToPath(new URL('../../../wwwroot/surface-app', import.meta.url)),
    emptyOutDir: true,
    lib: {
      entry: fileURLToPath(new URL('./src/main.ts', import.meta.url)),
      formats: ['iife'],
      name: 'CalloraSurface',
      fileName: () => 'surface.js',
    },
    rollupOptions: {
      output: {
        assetFileNames: (assetInfo) =>
          assetInfo.name?.endsWith('.css') ? 'surface.css' : assetInfo.name ?? 'asset',
      },
    },
  },
  test: { environment: 'happy-dom', globals: true },
})
