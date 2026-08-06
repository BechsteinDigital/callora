# @callora/surface-sdk

The typed contract and blessed Vite preset for building **Callora surface plugins** —
Vue components that dock into the surface runtime (`src/Surface.Rendering/Resources/app/surface`).

A surface plugin ships one self-registering IIFE bundle (`main.js` + optional `main.css`)
under `src/Resources/public/<surface>`. Vue stays **external** and is resolved at runtime
from the runtime's shared `window.CalloraVue`, so every plugin runs inside one Vue instance.

Licensed **Apache-2.0**.

## Installing

The package publishes its built artifact from `dist/` (JS + type declarations); `vue` and
`vite` are peer dependencies your plugin already has.

```bash
# From a registry, once published:
npm install --save-dev @callora/surface-sdk

# From the Callora repo today (build the SDK first, then depend on it by path):
npm run build            # in custom/surface-sdk/ → produces dist/
# in your plugin's package.json:
#   "dependencies": { "@callora/surface-sdk": "file:../../surface-sdk" }
```

## Building

```bash
npm run build       # tsc -p tsconfig.build.json → dist/ (js + .d.ts)
npm run typecheck   # tsc --noEmit
npm run test        # vitest
```

## Plugin entry (`main.ts`)

```ts
import { registerSurfaceView } from '@callora/surface-sdk'
import CallsPage from './CallsPage.vue' // a normal .vue — `import ... from 'vue'` resolves to CalloraVue

registerSurfaceView({ id: 'voip.calls', component: CallsPage })
```

- **App mode** — the runtime renders every registered view inside `#callora-app`.
- **Islands mode** — a view is mounted where the SSR content has
  `<div data-callora-island="voip.calls"></div>`.

The component receives the `SurfaceContext` (`{ workspaceKey, surfaceKey }`) as a `context` prop.

## Build config (`vite.config.ts`)

```ts
import { calloraSurfacePlugin } from '@callora/surface-sdk/vite-preset'

export default calloraSurfacePlugin({
  entry: 'src/Resources/app/surface/src/main.ts',
  name: 'CalloraVoipWorkspace',
})
```

The preset sets Vue external (`globals: { vue: 'CalloraVue' }`), an IIFE build with fixed
`main.js`/`main.css` names, and output to `src/Resources/public/<surface>`.

## Contract

- `SurfaceContext` — `{ workspaceKey, surfaceKey }`
- `SurfaceView` — `{ id, component, order? }`
- `registerSurfaceView(view)` — register with the runtime; a no-op with a warning (never
  throws) if the runtime is not present, so a plugin never breaks the shell.
