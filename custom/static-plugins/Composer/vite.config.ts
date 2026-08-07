import { calloraAdminPlugin } from '@callora/admin/vite-preset'

// Das vorgegebene Preset: Vue external → window.CalloraVue, ein IIFE (main.js/main.css),
// Ausgabe nach src/Resources/public/admin. `vite build` aus dem Plugin-Wurzelverzeichnis.
export default {
  ...calloraAdminPlugin({
    entry: 'src/Resources/app/admin/src/main.ts',
    name: 'CalloraComposerAdminUi',
  }),
  test: { environment: 'happy-dom', globals: true },
}
