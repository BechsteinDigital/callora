import { calloraAdminPlugin } from '@callora/admin/vite-preset'

// The blessed preset: Vue external → window.CalloraVue, one IIFE (main.js/main.css),
// output to src/Resources/public/admin. Run `vite build` from the plugin root.
export default calloraAdminPlugin({
  entry: 'src/Resources/app/admin/src/main.ts',
  name: 'CalloraCommunicationAdminUi',
})
