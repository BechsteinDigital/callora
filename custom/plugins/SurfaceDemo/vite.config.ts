import { calloraSurfacePlugin } from '@callora/surface-sdk/vite-preset'

// The blessed preset: Vue external → window.CalloraVue, one IIFE (main.js/main.css),
// output to src/Resources/public/workspace. Run `vite build` from the plugin root.
export default calloraSurfacePlugin({
  entry: 'src/Resources/app/workspace/src/main.ts',
  name: 'CalloraSurfaceDemo',
})
