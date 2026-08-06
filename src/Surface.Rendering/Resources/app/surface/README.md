# @callora/surface

The Callora surface runtime **and** the contract surface plugins compile against — one
package, one declaration. A surface plugin is a Vue bundle that docks into this runtime:
it registers views and blocks, reads the surface context, and collaborates with other
plugins over the context channel.

The contract used to live in a separate package (`custom/surface-sdk`) because the runtime
was private. That meant every type was declared twice and could drift without anything
noticing. Now the runtime itself is the package, so there is one declaration — the same
shape Umbraco uses for `@umbraco-cms/backoffice`, and `@callora/admin` for the admin shell.

A plugin ships one self-registering IIFE bundle (`main.js` + optional `main.css`) under
`src/Resources/public/<surface>`. Vue stays **external** and is resolved at runtime from
the shared `window.CalloraVue`, so every plugin runs inside one Vue instance — two Vues
would mean reactivity that silently does not cross the boundary.

Licensed **Apache-2.0**, deliberately and independently of the core's licence: a copyleft
frontend package would infect every plugin that embeds it, which would rule out a
commercial ecosystem before it starts.

## Installing

```bash
npm install --save-dev @callora/surface
```

In this repository, before publication, plugins consume it as a path dependency:

```jsonc
{
  "dependencies": {
    "@callora/surface": "file:../../../src/Surface.Rendering/Resources/app/surface"
  }
}
```

A `file:` dependency links the source directory, not a published tarball — so build the
library first, or the import resolves to nothing:

```bash
npm run build:lib   # in this directory → dist-lib/
```

## Registering a view

```ts
import { registerSurfaceView } from '@callora/surface'
import GreetingPage from './GreetingPage.vue'

registerSurfaceView({ id: 'demo.greeting', component: GreetingPage })
```

```vue
<script setup lang="ts">
import type { SurfaceViewProps } from '@callora/surface'

// Both props were always passed; this is the shape to annotate against.
defineProps<SurfaceViewProps>()
</script>
```

The `id` is the view id **and** the value of `data-callora-island` in server-rendered
markup. One identity: a view placed by an SSR template and one registered here are the
same thing.

## Registering a block

A block is a view with editor metadata — what the composer offers and what the runtime
renders. Registering it registers the view alongside.

```ts
import { registerBlock, registerBlockCategory } from '@callora/surface'

registerBlockCategory({ id: 'telephony', label: 'Telefonie', icon: 'phone' })

registerBlock({
  id: 'communication.call-list',
  label: 'Anrufliste',
  category: 'telephony',
  requires: ['communication.active-call/v1'],
  component: CallListBlock,
  controls: {
    title: { type: 'text', label: 'Überschrift' },
    accent: { type: 'colorToken', label: 'Akzent' },
  },
})
```

The category is a free string with its own registration point — a plugin that invents one
needs no change to the host. The **appearance** control types (`colorToken`,
`spacingToken`, `typeToken`, `variant`) are the exception to what a plugin may extend:
they pick from `--cal-*` roles and steps and nothing else, and a free colour picker
contributed there would undo that guarantee in a single registration.

## The context channel

Views collaborate over namespaced, versioned keys rather than by reaching into each other.
Take a scope in `setup()` and dispose it on unmount — a view that leaves the page must not
keep a key claimed or keep receiving values into a component that no longer exists.

```ts
import { createSurfaceContextScope } from '@callora/surface/context'
import { onUnmounted } from 'vue'

const scope = createSurfaceContextScope()
const publisher = scope.publish({ key: 'crm.lead-selection/v1', cardinality: 'single' })
scope.subscribe('communication.active-call/v1', (call) => { /* … */ })
onUnmounted(() => scope.dispose())
```

## The Vite preset

```ts
import { calloraSurfacePlugin } from '@callora/surface/vite-preset'

export default calloraSurfacePlugin({
  entry: 'src/Resources/app/surface/src/main.ts',
  name: 'CalloraSurfaceDemo',
})
```

It sets Vue external (`globals: { vue: 'CalloraVue' }`), an IIFE build with fixed
`main.js`/`main.css` names, and output to `src/Resources/public/<surface>` — the one
bundle shape the runtime loads.

## Entry points

| Import | What is in it |
|---|---|
| `@callora/surface` | Views, blocks, context types, registration |
| `@callora/surface/context` | The context channel and its scope |
| `@callora/surface/vite-preset` | The blessed Vite config |
| `@callora/surface/tokens.scss` | The design tokens, for a plugin styling beyond them |

Everything a plugin needs is here. Anything else under `src/` is the runtime's own
business — reachable inside the project, but not through a package entry point.
