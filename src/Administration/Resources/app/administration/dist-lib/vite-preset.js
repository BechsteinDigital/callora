import r from "@vitejs/plugin-vue";
function a(e) {
  return {
    plugins: [r()],
    // A plugin bundle is built once and served as-is; there is no environment to read at
    // runtime, and Vue's browser build branches on this value.
    define: { "process.env.NODE_ENV": '"production"' },
    build: {
      outDir: e.outDir ?? "src/Resources/public/admin",
      emptyOutDir: !0,
      cssCodeSplit: !1,
      lib: {
        entry: e.entry,
        formats: ["iife"],
        name: e.name,
        fileName: () => "main.js"
      },
      rollupOptions: {
        external: ["vue"],
        output: {
          globals: { vue: "CalloraAdmin.vue" },
          assetFileNames: (i) => i.names.some((n) => n.endsWith(".css")) ? "main.css" : i.names[0] ?? "[name][extname]"
        }
      }
    }
  };
}
export {
  a as calloraAdminPlugin
};
