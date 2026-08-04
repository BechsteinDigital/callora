import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  base: '/admin/',
  resolve: { alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) } },
  // Dart Sass deprecated its legacy JS API; the modern compiler is also the
  // faster one, and it keeps the build output free of deprecation noise.
  css: { preprocessorOptions: { scss: { api: 'modern-compiler' } } },
  build: {
    outDir: fileURLToPath(new URL('../../../wwwroot/admin', import.meta.url)),
    emptyOutDir: true,
  },
  server: {
    port: 5273,
    proxy: {
      '/api': 'http://localhost:5000',
      '/workspace': 'http://localhost:5000',
    },
  },
  test: { environment: 'happy-dom', globals: true },
})
