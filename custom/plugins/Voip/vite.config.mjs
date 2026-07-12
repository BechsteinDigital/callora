import { defineConfig } from "vite";
import vue from "@vitejs/plugin-vue";

// Plugin-UI-Build (Shopware-analog): Quellen unter src/Resources/app/<surface>/src,
// Kompilat unter src/Resources/public/<surface> — nur das Kompilat wird
// publiziert/ausgeliefert. Vue bleibt external und kommt zur Laufzeit aus dem
// CalloraVue-Global der Shell, damit Komponenten in derselben Vue-Instanz laufen.
export default defineConfig({
  plugins: [vue()],
  build: {
    lib: {
      entry: "src/Resources/app/workspace/src/main.ts",
      formats: ["iife"],
      name: "CalloraVoipWorkspace",
      fileName: () => "main.js"
    },
    outDir: "src/Resources/public/workspace",
    emptyOutDir: true,
    rollupOptions: {
      external: ["vue"],
      output: {
        globals: { vue: "CalloraVue" },
        assetFileNames: (assetInfo) =>
          assetInfo.name?.endsWith(".css") ? "main.css" : assetInfo.name ?? "asset"
      }
    }
  }
});
