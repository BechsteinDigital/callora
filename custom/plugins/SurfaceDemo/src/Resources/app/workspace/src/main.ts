import { registerSurfaceView } from '@callora/surface-sdk'
import GreetingPage from './GreetingPage.vue'

// Register this plugin's view with the surface runtime. The runtime renders it as the
// whole app (into #callora-app) or into a matching data-callora-island placeholder.
registerSurfaceView({
  id: 'surface-demo.greeting',
  component: GreetingPage,
})
