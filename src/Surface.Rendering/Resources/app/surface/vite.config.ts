import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import { browserProcessDefinitions } from './src/build-constants'

// The surface runtime is the neutral grundgerüst that the SSR SurfaceShell loads
// into #callora-app (ADR-014 §8). It builds as ONE self-mounting IIFE bundle with
// fixed names (surface.js / surface.css) — no index.html of its own; the server-
// rendered shell is the document. Vue is BUNDLED here (the runtime owns the single
// instance and re-exposes it as window.CalloraVue); plugin bundles keep Vue external
// and resolve it from that global, so everything shares one Vue.
export default defineConfig({
  plugins: [vue()],
  base: '/surface-app/',
  // Vue's browser bundle still contains CommonJS-style environment guards.
  // The Surface is loaded directly as an IIFE (there is no Node/process shim),
  // so leaving these references intact aborts the shell before it can mount.
  define: browserProcessDefinitions,
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
  test: {
    environment: 'happy-dom',
    globals: true,
    // Unit tests assert that plugin <script>/<link> tags get injected; they must never
    // actually fetch/execute them (there is no server), so disable resource loading.
    environmentOptions: {
      happyDOM: { settings: { disableJavaScriptFileLoading: true, disableCSSFileLoading: true } },
    },
  },
  // The golden-path test imports the SurfaceDemo plugin's BUILT bundle as raw text to prove
  // the whole chain against a real artifact. That path is outside this project's root, which
  // Vite's filesystem guard denies by default — the hardening that closed the path-traversal
  // advisory. Allowing the repository root re-opens exactly that read. It applies to the dev
  // server and the test run; the production build is a library build with neither.
  server: {
    fs: { allow: [fileURLToPath(new URL('../../../../..', import.meta.url))] },
  },
})
