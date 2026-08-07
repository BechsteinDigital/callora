import { calloraSurfacePlugin } from '@callora/surface/vite-preset'

// The blessed preset: Vue external → window.CalloraVue, one IIFE (main.js/main.css),
// output to src/Resources/public/surface. A second config next to the admin one because
// a plugin ships two bundles for two runtimes, not one bundle that guesses where it is.
export default calloraSurfacePlugin({
  entry: 'src/Resources/app/surface/src/main.ts',
  name: 'CalloraCommunicationSurfaceUi',
})
