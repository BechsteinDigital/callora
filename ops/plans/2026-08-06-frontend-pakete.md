# Frontend-Pakete `@callora/admin` und `@callora/surface` — Umsetzungsplan

> **Für agentische Bearbeiter:** ERFORDERLICHE SUB-SKILL: `superpowers:subagent-driven-development`
> (empfohlen) oder `superpowers:executing-plans`. Schritte nutzen Checkbox-Syntax (`- [ ]`).

**Ziel:** Aus Admin-Shell und Surface-Runtime je ein publizierbares npm-Paket machen, gegen das
Plugin-Autoren typsicher bauen — mit einem generierten Extension-Point-Katalog, der einen
Tippfehler zum Compiler-Fehler macht.

**Architektur:** Umbracos Muster — das Paket lebt im Modulverzeichnis und wird von dort
publiziert, mit Unterpfad-Exporten statt vieler Pakete. Zwei Vite-Konfigurationen je Modul: eine
baut die App bzw. Runtime, eine die Bibliothek. Kein geteilter Kern, kein `sdk/`-Verzeichnis;
Begründung in [Frontend-Paketstruktur](../analysis/2026-08-06-frontend-paketstruktur.md).

**Tech-Stack:** TypeScript 5.6, Vue 3.5, Vite 6, Vitest 4, `@vue/test-utils`, happy-dom 20,
.NET 10 / xUnit für den Konsistenztest.

**Vorarbeiten erledigt:** Loader-Angleich (`59809c6`), npm-Advisories (`e923690`),
`ui-core`-Rückbau (`77cceb6`).

---

## Was gegenüber dem verworfenen Entwurf entfällt

Der erste Plan sah einen flächenneutralen Kern vor. Mit dessen Wegfall entfallen ersatzlos:

| Entfällt | War nötig für |
|---|---|
| **Link-Port** (`provideLinkAdapter`) | `CalButton` ohne `vue-router` — in einem Admin-Paket darf es `vue-router` importieren |
| **Ein gemeinsames Vue-Global** | Blöcke, die in beiden Runtimes laufen — jedes Paket behält sein eigenes |
| **HTTP-Port mit austauschbarem Transport** | derselbe Grund; jedes Paket hat seinen eigenen `apiFetch` |
| **Drift-Test SDK ↔ Runtime** | die Duplikation verschwindet, weil beide dasselbe Paket werden |
| **Komponenten-Umzug in ein drittes Paket** | — |

Übrig bleibt, was echten Wert hat: der typsichere Vertrag, der generierte Katalog und die
Server↔Client-Bindung.

---

## Dateistruktur

**`src/Administration/Resources/app/administration/`** → `@callora/admin`

| Datei | Verantwortung |
|---|---|
| `package.json` | Paket-Identität, Unterpfad-Exporte, `files`, Lizenz |
| `LICENSE` | Apache-2.0 |
| `vite.lib.config.ts` | **neu** — Bibliotheks-Build neben dem App-Build |
| `src/public/index.ts` | Barrel: der öffentliche Vertrag des Pakets |
| `src/public/extensions.ts` | typisierte Registrierung + `catalog.generated` |
| `src/public/components.ts` | Barrel über die `Cal*`-Primitive |
| `src/public/tokens.ts` | re-exportiert `core/design/tokens` |
| `src/public/patterns.ts` | `CalListPage` u. a. |
| `src/core/extensions/catalog.generated.ts` | **generiert** — `AdminSlot`/`AdminHook`-Unions |
| `src/core/extensions/catalog.json` | **generiert** — Katalog mit Fundstelle |
| `src/core/extensions/scan-extension-points.ts` | reiner Scanner |
| `src/core/extensions/replaceable.ts` | `defineReplaceable`/`useComponent` |
| `src/core/patterns/CalListPage.vue` | Muster mit eingebauten Slots |
| `bin/generate-catalog.mjs` | CLI für den Katalog |
| `vite-preset.ts` | `calloraAdminPlugin()` für Plugin-Bundles |

**`src/Surface.Rendering/Resources/app/surface/`** → `@callora/surface`

| Datei | Verantwortung |
|---|---|
| `package.json` | Paket-Identität, Unterpfade, Lizenz |
| `LICENSE` | Apache-2.0 |
| `vite.lib.config.ts` | **neu** |
| `src/public/index.ts` | Barrel |
| `src/vite-preset.ts` | aus `custom/surface-sdk` übernommen |
| `src/surface-*.ts` | bestehende Implementierungen, jetzt zugleich der Vertrag |

**Entfällt:** `custom/surface-sdk/` (geht in die Runtime auf).

---

# Phase 1 — `@callora/admin`

### Task 1: Paket-Identität und Lizenz

**Dateien:**
- Ändern: `src/Administration/Resources/app/administration/package.json`
- Anlegen: `src/Administration/Resources/app/administration/LICENSE`
- Anlegen: `src/Administration/Resources/app/administration/src/public/index.ts`
- Test: `src/Administration/Resources/app/administration/src/public/index.test.ts`

- [ ] **Schritt 1: Den fehlschlagenden Test schreiben**

```ts
import { describe, expect, it } from 'vitest'
import { ADMIN_PACKAGE_VERSION } from './index'

describe('@callora/admin', () => {
  it('exposes its contract version so a plugin can refuse an incompatible host', () => {
    expect(ADMIN_PACKAGE_VERSION).toMatch(/^\d+\.\d+\.\d+$/)
  })
})
```

- [ ] **Schritt 2: Test ausführen und Fehlschlag prüfen**

```bash
cd src/Administration/Resources/app/administration && npx vitest run src/public/index.test.ts
```

Erwartet: FAIL mit `Failed to resolve import "./index"`.

- [ ] **Schritt 3: Barrel und Paket-Metadaten anlegen**

`src/public/index.ts`:

```ts
/**
 * The public contract of @callora/admin.
 *
 * Everything reachable from here is what a plugin may rely on; everything else in this project
 * is the shell's own business and may change without notice. The subpath exports in
 * package.json point at the files next to this one, so a plugin imports
 * `@callora/admin/extensions` rather than reaching into src/.
 */

/** Contract version of this package, so a plugin can refuse an incompatible host. */
export const ADMIN_PACKAGE_VERSION = '0.1.0'
```

In `package.json`: `"private": true` entfernen und ergänzen:

```json
{
  "name": "@callora/admin",
  "version": "0.1.0",
  "description": "Typed contract for building Callora admin plugins: extension points, primitives, patterns.",
  "license": "Apache-2.0",
  "exports": {
    ".": { "types": "./dist-lib/public/index.d.ts", "import": "./dist-lib/public/index.js" },
    "./extensions": { "types": "./dist-lib/public/extensions.d.ts", "import": "./dist-lib/public/extensions.js" },
    "./components": { "types": "./dist-lib/public/components.d.ts", "import": "./dist-lib/public/components.js" },
    "./tokens": { "types": "./dist-lib/public/tokens.d.ts", "import": "./dist-lib/public/tokens.js" },
    "./patterns": { "types": "./dist-lib/public/patterns.d.ts", "import": "./dist-lib/public/patterns.js" },
    "./tokens.scss": "./src/core/design/tokens.scss",
    "./vite-preset": { "types": "./dist-lib/vite-preset.d.ts", "import": "./dist-lib/vite-preset.js" }
  },
  "files": ["dist-lib", "src/core/design/tokens.scss", "LICENSE", "README.md"],
  "publishConfig": { "access": "public" }
}
```

`LICENSE`: den Apache-2.0-Volltext anlegen (identisch zu `custom/surface-sdk`, falls dort
vorhanden — sonst von apache.org).

- [ ] **Schritt 4: Test ausführen und Erfolg prüfen**

Erwartet: PASS.

- [ ] **Schritt 5: Commit**

```bash
git add -A src/Administration/Resources/app/administration
git commit -m "build(admin): Paket-Identität und Apache-2.0-Lizenz"
```

---

### Task 2: Extension-Point-Scanner

**Dateien:**
- Anlegen: `src/core/extensions/scan-extension-points.ts`
- Test: `src/core/extensions/scan-extension-points.test.ts`

Rein: Quelltext rein, Punkteliste raus — testbar ohne Dateisystem.

- [ ] **Schritt 1: Den fehlschlagenden Test schreiben**

```ts
import { describe, expect, it } from 'vitest'
import { scanExtensionPoints } from './scan-extension-points'

describe('scanExtensionPoints', () => {
  it('finds a slot declared in a template', () => {
    expect(scanExtensionPoints('<ExtensionSlot name="users.list.toolbar" />', 'x.vue')).toEqual([
      { kind: 'slot', name: 'users.list.toolbar', file: 'x.vue' },
    ])
  })

  it('finds a slot that also passes a context', () => {
    expect(scanExtensionPoints('<ExtensionSlot name="users.detail.fields" :ctx="{ userId }" />', 'x.vue')).toEqual([
      { kind: 'slot', name: 'users.detail.fields', file: 'x.vue' },
    ])
  })

  it('finds a hook invoked with a literal name', () => {
    expect(scanExtensionPoints("await runHook('users.before-save', draft)", 'x.vue')).toEqual([
      { kind: 'hook', name: 'users.before-save', file: 'x.vue' },
    ])
  })

  it('records a template-literal hook as a pattern, because the value is only known at runtime', () => {
    expect(scanExtensionPoints('await runHook(`plugins.before-${verb}`, {})', 'x.vue')).toEqual([
      { kind: 'hook', name: 'plugins.before-*', file: 'x.vue', dynamic: true },
    ])
  })

  it('deduplicates a point declared twice in one file', () => {
    expect(scanExtensionPoints('<ExtensionSlot name="a.b" /><ExtensionSlot name="a.b" />', 'x.vue')).toHaveLength(1)
  })

  it('ignores the ExtensionSlot component definition itself', () => {
    expect(scanExtensionPoints('defineProps<{ name: string; ctx?: unknown }>()', 'ExtensionSlot.vue')).toEqual([])
  })

  it('returns nothing for a file with no points', () => {
    expect(scanExtensionPoints('export const x = 1', 'x.ts')).toEqual([])
  })
})
```

- [ ] **Schritt 2: Test ausführen und Fehlschlag prüfen**

Erwartet: FAIL — Modul fehlt.

- [ ] **Schritt 3: Implementierung schreiben**

```ts
/**
 * Extracts the extension points a source file declares. Pure by design: the generator does the
 * file walking, this does the reading — so the interesting part is testable without a filesystem.
 *
 * Deliberately regex-based rather than AST-based. The two call shapes are fixed by convention
 * (`<ExtensionSlot name="…">` and `runHook('…')`), a parser would pull in a heavy dependency for
 * no gain, and a missed point is caught by the catalog completeness test.
 */

export type ExtensionPointKind = 'slot' | 'hook'

export interface ExtensionPoint {
  readonly kind: ExtensionPointKind
  /** Dotted name, or a `*`-suffixed pattern when the call interpolates. */
  readonly name: string
  readonly file: string
  /** True when the name is assembled at runtime and only its prefix is known. */
  readonly dynamic?: boolean
}

const SLOT = /<ExtensionSlot[^>]*\sname="([^"]+)"/g
const HOOK_LITERAL = /runHook\(\s*['"]([^'"]+)['"]/g
const HOOK_TEMPLATE = /runHook\(\s*`([^`$]*)\$\{/g

export function scanExtensionPoints(source: string, file: string): ExtensionPoint[] {
  const found: ExtensionPoint[] = []
  const seen = new Set<string>()

  const add = (point: ExtensionPoint): void => {
    const key = `${point.kind}:${point.name}`
    if (!seen.has(key)) {
      seen.add(key)
      found.push(point)
    }
  }

  for (const [, name] of source.matchAll(SLOT)) add({ kind: 'slot', name, file })
  for (const [, name] of source.matchAll(HOOK_LITERAL)) add({ kind: 'hook', name, file })
  // `runHook(\`plugins.before-${verb}\`)` cannot be resolved statically. Recording the prefix as
  // a pattern is more useful than dropping it: a plugin author sees that the family exists.
  for (const [, prefix] of source.matchAll(HOOK_TEMPLATE)) {
    add({ kind: 'hook', name: `${prefix}*`, file, dynamic: true })
  }

  return found
}
```

- [ ] **Schritt 4: Test ausführen und Erfolg prüfen**

Erwartet: PASS (7 Tests).

- [ ] **Schritt 5: Commit**

```bash
git commit -am "feat(admin): Scanner für die Extension-Points der Shell"
```

---

### Task 3: Katalog-Generator und Literal-Unions

**Dateien:**
- Anlegen: `bin/generate-catalog.mjs`
- Anlegen (generiert): `src/core/extensions/catalog.generated.ts`, `catalog.json`
- Test: `src/core/extensions/catalog.test.ts`
- Ändern: `package.json` (Skript), `.github/workflows/ci.yml` (Drift-Gate)

Dies ist der Punkt, an dem wir die Peers schlagen: Umbraco, Shopware und ABP registrieren
Extension-Points über Strings; ein Tippfehler ist dort ein stiller No-Op.

- [ ] **Schritt 1: Generator schreiben**

```js
#!/usr/bin/env node
/**
 * Walks the shell sources, collects every extension point and writes two artifacts:
 *
 *   catalog.json          — name, kind, declaring file, for tooling and docs
 *   catalog.generated.ts  — literal unions, so a typo in a plugin is a compile error
 *
 * Both are committed. CI regenerates and fails on a diff, so the catalog cannot drift from the
 * shell without somebody noticing.
 */
import { readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs'
import { join, relative, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { scanExtensionPoints } from '../src/core/extensions/scan-extension-points.ts'

const root = resolve(process.argv[2] ?? 'src')
const outDir = resolve(fileURLToPath(new URL('../src/core/extensions', import.meta.url)))

function* walk(dir) {
  for (const entry of readdirSync(dir)) {
    const path = join(dir, entry)
    if (statSync(path).isDirectory()) yield* walk(path)
    else if (/\.(vue|ts)$/.test(entry) && !/\.(test|spec)\.ts$/.test(entry)) yield path
  }
}

const points = []
for (const file of walk(root)) {
  points.push(...scanExtensionPoints(readFileSync(file, 'utf8'), relative(root, file)))
}

const byKind = (kind) => [...new Set(points.filter((p) => p.kind === kind).map((p) => p.name))].sort()
const union = (values) => (values.length ? values.map((v) => `  | '${v}'`).join('\n') : '  never')

writeFileSync(
  join(outDir, 'catalog.generated.ts'),
  `// GENERATED by bin/generate-catalog.mjs — do not edit by hand.
// Regenerate with: npm run generate:catalog

/** Every slot the shell renders. A typo is a compile error, not a silent no-op. */
export type AdminSlot =
${union(byKind('slot'))}

/** Every hook the shell runs. Names ending in '*' are assembled at runtime. */
export type AdminHook =
${union(byKind('hook'))}
`,
)

writeFileSync(
  join(outDir, 'catalog.json'),
  `${JSON.stringify(
    { slots: points.filter((p) => p.kind === 'slot'), hooks: points.filter((p) => p.kind === 'hook') },
    null,
    2,
  )}\n`,
)

console.log(`Katalog: ${byKind('slot').length} Slots, ${byKind('hook').length} Hooks.`)
```

In `package.json`:

```json
"generate:catalog": "node --experimental-strip-types bin/generate-catalog.mjs src"
```

Node ≥ 22.6 nötig (ab 22.18 voreingestellt); `scan-extension-points.ts` nutzt nur `interface`
und `type`, also genau die strippbare Teilmenge. `engines` entsprechend setzen.

- [ ] **Schritt 2: Generator ausführen**

```bash
cd src/Administration/Resources/app/administration && npm run generate:catalog
```

Erwartet: rund 29 Slots und 40 Hooks, zwei neue Dateien.

- [ ] **Schritt 3: Test schreiben**

```ts
import { describe, expect, it } from 'vitest'
import catalog from './catalog.json'

describe('extension point catalog', () => {
  it('contains the slots the shell actually renders', () => {
    const names = catalog.slots.map((s) => s.name)
    expect(names).toContain('users.list.toolbar')
    expect(names).toContain('dashboard.metrics')
  })

  it('contains the hooks the shell actually runs', () => {
    const names = catalog.hooks.map((h) => h.name)
    expect(names).toContain('users.before-save')
    expect(names).toContain('users.after-save')
  })

  it('names every point in the {module}.{…} convention, so the catalog stays navigable', () => {
    for (const point of [...catalog.slots, ...catalog.hooks]) {
      expect(point.name).toMatch(/^[a-z][a-z0-9-]*(\.[a-z0-9-]+)+\*?$/)
    }
  })

  it('attributes every point to the file that declares it', () => {
    for (const point of [...catalog.slots, ...catalog.hooks]) {
      expect(point.file).toMatch(/\.(vue|ts)$/)
    }
  })
})
```

- [ ] **Schritt 4: Tests ausführen**

Erwartet: PASS. Schlägt der Konventionstest fehl, ist ein Punkt in der Shell falsch benannt —
ein echter Fund: dort umbenennen und neu generieren.

- [ ] **Schritt 5: CI-Gate gegen Drift**

Im `admin-frontend`-Job nach `Install`:

```yaml
      - name: Katalog aktuell?
        run: |
          npm run generate:catalog
          git diff --exit-code src/core/extensions/catalog.generated.ts src/core/extensions/catalog.json
```

- [ ] **Schritt 6: Commit**

```bash
git add -A && git commit -m "feat(admin): Extension-Point-Katalog generieren und gegen Drift sichern"
```

---

### Task 4: Typisierte Registrierungs-API

**Dateien:**
- Anlegen: `src/public/extensions.ts`
- Test: `src/public/extensions.test.ts`

Ersetzt das von Hand abgetippte Interface in
`custom/static-plugins/Communication/src/Resources/app/admin/src/main.ts`.

- [ ] **Schritt 1: Den fehlschlagenden Test schreiben**

```ts
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { h } from 'vue'
import { registerExtension, registerHook, registerPage, registerService, resolveAdminApi } from './extensions'

type Global = Record<string, unknown>
const Dummy = { setup: () => () => h('div') }

describe('admin registration API', () => {
  beforeEach(() => {
    delete (globalThis as Global).CalloraAdmin
  })

  it('forwards a slot registration to the shell', () => {
    const spy = vi.fn()
    ;(globalThis as Global).CalloraAdmin = { registerExtension: spy }

    registerExtension('users.list.toolbar', Dummy, 10)

    expect(spy).toHaveBeenCalledWith('users.list.toolbar', Dummy, 10)
  })

  it('forwards a hook registration to the shell', () => {
    const spy = vi.fn()
    ;(globalThis as Global).CalloraAdmin = { registerHook: spy }
    const handler = () => {}

    registerHook('users.before-save', handler)

    expect(spy).toHaveBeenCalledWith('users.before-save', handler, undefined)
  })

  it('forwards a service override to the shell', () => {
    const spy = vi.fn()
    ;(globalThis as Global).CalloraAdmin = { registerService: spy }
    const impl = { list: async () => [] }

    registerService('usersApi', impl, { priority: 5 })

    expect(spy).toHaveBeenCalledWith('usersApi', impl, { priority: 5 })
  })

  it('registers a full plugin page under the slot the shell routes to', () => {
    const spy = vi.fn()
    ;(globalThis as Global).CalloraAdmin = { registerExtension: spy }

    registerPage('communication', Dummy)

    expect(spy).toHaveBeenCalledWith('extension.page.communication', Dummy, undefined)
  })

  it('warns instead of throwing when the shell is absent, so a plugin never breaks it', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})

    registerExtension('users.list.toolbar', Dummy)

    expect(warn).toHaveBeenCalledWith(expect.stringContaining('admin shell not initialised'))
    warn.mockRestore()
  })

  it('returns undefined from resolveAdminApi before the shell installed it', () => {
    expect(resolveAdminApi()).toBeUndefined()
  })
})
```

- [ ] **Schritt 2: Test ausführen und Fehlschlag prüfen**

- [ ] **Schritt 3: Implementierung schreiben**

```ts
import type { Component } from 'vue'
import type { AdminHook, AdminSlot } from '@/core/extensions/catalog.generated'

/**
 * The typed contract an admin plugin registers against.
 *
 * Until now a plugin hand-copied this interface from the shell's loader and passed slot names as
 * free strings — a typo was a silent no-op. The names are generated from the shell, so a wrong
 * one is a compile error.
 *
 * Every call is a no-op with a warning when the shell is absent, never a throw: a plugin must
 * not be able to break the shell it is a guest in.
 */

export interface HookContext<T> {
  /** Mutable: a handler may enrich the payload before the action proceeds. */
  readonly payload: T
  /** Aborts the action; the first cancel wins and stops later handlers. */
  cancel(reason?: string): void
}

export type HookHandler<T> = (ctx: HookContext<T>) => void | Promise<void>

export interface ServiceMeta {
  /** Highest priority wins; ties resolve to the last registration. */
  readonly priority?: number
}

/** The shell's global. The owning pluginId is injected by the loader, never by the plugin. */
export interface CalloraAdminApi {
  registerExtension(slot: string, component: Component, order?: number): void
  registerHook<T>(name: string, handler: HookHandler<T>, order?: number): void
  registerService<T>(key: string, implementation: T, meta?: ServiceMeta): void
  getExtensions(slot: string): Component[]
}

export function resolveAdminApi(): CalloraAdminApi | undefined {
  return (globalThis as Record<string, unknown>).CalloraAdmin as CalloraAdminApi | undefined
}

function missing(what: string): void {
  console.warn(`[callora-admin] admin shell not initialised; ${what} was not registered.`)
}

/** Contributes a component to a shell slot. Slots are additive — every registration renders. */
export function registerExtension(slot: AdminSlot, component: Component, order?: number): void {
  const api = resolveAdminApi()
  if (!api) return missing(`slot "${slot}"`)
  api.registerExtension(slot, component, order)
}

/** Intervenes around a shell action. A `before` handler may mutate the payload or cancel. */
export function registerHook<T>(name: AdminHook, handler: HookHandler<T>, order?: number): void {
  const api = resolveAdminApi()
  if (!api) return missing(`hook "${name}"`)
  api.registerHook(name, handler, order)
}

/**
 * Replaces a named shell service. Exclusive — only one implementation wins — so a conflict is
 * surfaced by the shell rather than silently swallowed.
 */
export function registerService<T>(key: string, implementation: T, meta?: ServiceMeta): void {
  const api = resolveAdminApi()
  if (!api) return missing(`service "${key}"`)
  api.registerService(key, implementation, meta)
}

/**
 * Contributes a plugin's full admin page, rendered by the shell at /extensions/{pluginId}.
 *
 * Deliberately its own function rather than a helper passed to registerExtension: that slot name
 * only exists at runtime, so accepting it there would mean widening the parameter to
 * `AdminSlot | string` — which TypeScript collapses to `string`, silently removing the typo
 * protection from every other slot.
 */
export function registerPage(pluginId: string, component: Component, order?: number): void {
  const api = resolveAdminApi()
  if (!api) return missing(`page for "${pluginId}"`)
  api.registerExtension(`extension.page.${pluginId}`, component, order)
}

export type { AdminHook, AdminSlot }
```

- [ ] **Schritt 4: Test ausführen und Erfolg prüfen**

Erwartet: PASS (6 Tests).

- [ ] **Schritt 5: Commit**

---

### Task 5: Ersetzbare Komponenten

**Dateien:**
- Anlegen: `src/core/extensions/replaceable.ts`
- Test: `src/core/extensions/replaceable.test.ts`

Ersetzung nur an markierten Punkten, Prop-Vertrag als Grenze, Konflikte diagnostizierbar —
dasselbe Muster wie `useService`/`getServiceConflicts`. Blanko-Override wäre das, was der
Registry-Kommentar ablehnt („no internal-structure coupling").

- [ ] **Schritt 1: Den fehlschlagenden Test schreiben**

```ts
import { beforeEach, describe, expect, it } from 'vitest'
import { h } from 'vue'
import {
  defineReplaceable,
  getComponentConflicts,
  replaceComponent,
  resetReplacements,
  useComponent,
} from './replaceable'

const Base = { name: 'Base', setup: () => () => h('div', 'base') }
const Custom = { name: 'Custom', setup: () => () => h('div', 'custom') }
const Other = { name: 'Other', setup: () => () => h('div', 'other') }

describe('replaceable components', () => {
  beforeEach(resetReplacements)

  it('resolves to the original when nobody replaced it', () => {
    expect(useComponent(defineReplaceable('cal.data-table', Base))).toBe(Base)
  })

  it('resolves to a registered replacement', () => {
    const token = defineReplaceable('cal.data-table', Base)
    replaceComponent('cal.data-table', Custom)
    expect(useComponent(token)).toBe(Custom)
  })

  it('lets the highest priority win', () => {
    const token = defineReplaceable('cal.data-table', Base)
    replaceComponent('cal.data-table', Custom, { priority: 1, pluginId: 'a' })
    replaceComponent('cal.data-table', Other, { priority: 5, pluginId: 'b' })
    expect(useComponent(token)).toBe(Other)
  })

  it('lets the later registration win on equal priority, matching the loader order', () => {
    const token = defineReplaceable('cal.data-table', Base)
    replaceComponent('cal.data-table', Custom, { pluginId: 'a' })
    replaceComponent('cal.data-table', Other, { pluginId: 'b' })
    expect(useComponent(token)).toBe(Other)
  })

  it('reports a conflict so an operator sees which plugin was shadowed', () => {
    defineReplaceable('cal.data-table', Base)
    replaceComponent('cal.data-table', Custom, { pluginId: 'a' })
    replaceComponent('cal.data-table', Other, { pluginId: 'b' })
    expect(getComponentConflicts()).toEqual([
      { key: 'cal.data-table', activePluginId: 'b', shadowedPluginIds: ['a'] },
    ])
  })

  it('reports no conflict for a single replacement', () => {
    defineReplaceable('cal.data-table', Base)
    replaceComponent('cal.data-table', Custom, { pluginId: 'a' })
    expect(getComponentConflicts()).toEqual([])
  })

  it('carries its key on the token, so a consumer cannot resolve the wrong one', () => {
    expect(defineReplaceable('cal.dialog', Base).__calloraComponentKey).toBe('cal.dialog')
  })
})
```

- [ ] **Schritt 2: Test ausführen und Fehlschlag prüfen**

- [ ] **Schritt 3: Implementierung schreiben**

```ts
import type { Component } from 'vue'

/**
 * Component replacement — deliberately NOT blanket override. Shopware-style
 * `Component.override` couples a plugin to the internal structure of what it overrides; here a
 * component declares that it is replaceable, and its prop contract is the boundary. Where a
 * named slot suffices, that stays the better answer and replacement is the exception.
 *
 * Mirrors the service registry: exclusive, priority-ordered, conflicts surfaced.
 */

export type ReplaceableComponent<T extends Component> = T & { readonly __calloraComponentKey: string }

export interface ReplacementMeta {
  readonly pluginId?: string | null
  readonly priority?: number
}

interface Registration {
  readonly pluginId: string | null
  readonly priority: number
  readonly implementation: Component
}

const registrations = new Map<string, Registration[]>()

/** Marks a component as replaceable and brands it with its key. */
export function defineReplaceable<T extends Component>(key: string, implementation: T): ReplaceableComponent<T> {
  return Object.assign(implementation, { __calloraComponentKey: key }) as ReplaceableComponent<T>
}

export function replaceComponent(key: string, implementation: Component, meta: ReplacementMeta = {}): void {
  const list = registrations.get(key) ?? []
  list.push({ pluginId: meta.pluginId ?? null, priority: meta.priority ?? 0, implementation })
  registrations.set(key, list)
}

function winner(key: string): Registration | undefined {
  const list = registrations.get(key)
  if (!list || list.length === 0) return undefined
  // `>=` lets a later equal-priority registration win — deterministic given the loader's
  // ordered, sequential plugin loading.
  return list.reduce((best, current) => (current.priority >= best.priority ? current : best))
}

/** Resolves the component to render: the winning replacement, or the original. */
export function useComponent<T extends Component>(token: ReplaceableComponent<T>): Component {
  return winner(token.__calloraComponentKey)?.implementation ?? token
}

export interface ComponentConflict {
  readonly key: string
  readonly activePluginId: string | null
  readonly shadowedPluginIds: (string | null)[]
}

/** A key replaced by more than one plugin — an operator has to be able to see that. */
export function getComponentConflicts(): ComponentConflict[] {
  const conflicts: ComponentConflict[] = []
  for (const [key, list] of registrations) {
    if (list.length < 2) continue
    const active = winner(key)
    conflicts.push({
      key,
      activePluginId: active?.pluginId ?? null,
      shadowedPluginIds: list.filter((r) => r !== active).map((r) => r.pluginId),
    })
  }
  return conflicts
}

/** Test/hot-reload aid — clears all replacements. */
export function resetReplacements(): void {
  registrations.clear()
}
```

- [ ] **Schritt 4: Test ausführen und Erfolg prüfen** — PASS (7 Tests)

- [ ] **Schritt 5: Commit**

---

### Task 6: Muster-Ebene — `CalListPage`

**Dateien:**
- Anlegen: `src/core/patterns/CalListPage.vue`
- Test: `src/core/patterns/CalListPage.test.ts`
- Ändern: `src/modules/webhooks/WebhooksListView.vue` (erster Konsument)

Alle fünfzehn ListViews bauen dieselbe Anordnung von Hand und müssen jeweils daran denken, ihre
Extension-Slots zu platzieren. Das Muster bringt sie mit — Extension-Punkte entstehen dann durch
Verwendung, nicht durch Disziplin.

- [ ] **Schritt 1: Den fehlschlagenden Test schreiben**

```ts
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { h } from 'vue'
import CalListPage from './CalListPage.vue'

type Global = Record<string, unknown>
const Marker = { setup: () => () => h('span', { class: 'marker' }, 'x') }

describe('CalListPage', () => {
  beforeEach(() => {
    delete (globalThis as Global).CalloraAdmin
  })

  it('renders its title', () => {
    const wrapper = mount(CalListPage, { props: { module: 'users', title: 'Benutzer' } })
    expect(wrapper.text()).toContain('Benutzer')
  })

  it('renders the default slot as the page body', () => {
    const wrapper = mount(CalListPage, {
      props: { module: 'users', title: 'Benutzer' },
      slots: { default: '<p class="body">Tabelle</p>' },
    })
    expect(wrapper.find('.body').exists()).toBe(true)
  })

  it('brings its toolbar extension slot, derived from the module name', () => {
    const getExtensions = vi.fn((slot: string) => (slot === 'users.list.toolbar' ? [Marker] : []))
    ;(globalThis as Global).CalloraAdmin = { getExtensions }

    const wrapper = mount(CalListPage, { props: { module: 'users', title: 'Benutzer' } })

    expect(getExtensions).toHaveBeenCalledWith('users.list.toolbar')
    expect(wrapper.find('.marker').exists()).toBe(true)
  })

  it('derives the slot name from whatever module it is given', () => {
    const getExtensions = vi.fn(() => [])
    ;(globalThis as Global).CalloraAdmin = { getExtensions }

    mount(CalListPage, { props: { module: 'webhooks', title: 'Webhooks' } })

    expect(getExtensions).toHaveBeenCalledWith('webhooks.list.toolbar')
  })

  it('renders the actions slot beside the title', () => {
    const wrapper = mount(CalListPage, {
      props: { module: 'users', title: 'Benutzer' },
      slots: { actions: '<button class="new">Neu</button>' },
    })
    expect(wrapper.find('.new').exists()).toBe(true)
  })
})
```

- [ ] **Schritt 2: Test ausführen und Fehlschlag prüfen**

- [ ] **Schritt 3: Implementierung schreiben**

```vue
<template>
  <CalPage>
    <CalPageHeader :title="title" :description="description">
      <template #actions>
        <slot name="actions" />
      </template>
    </CalPageHeader>

    <ExtensionSlot :name="`${module}.list.toolbar`" :ctx="ctx" />

    <CalCard flush>
      <slot />
    </CalCard>
  </CalPage>
</template>

<script setup lang="ts">
import CalCard from '@/core/ui/CalCard.vue'
import CalPage from '@/core/ui/CalPage.vue'
import CalPageHeader from '@/core/ui/CalPageHeader.vue'
import ExtensionSlot from '@/core/extensions/ExtensionSlot.vue'

/**
 * The list-page pattern. Every list view built this arrangement by hand, and each one had to
 * remember to place its extension slots. Here the slots come WITH the pattern, so a new list
 * gets its extension points by construction instead of by discipline.
 *
 * Slot names follow the "{module}.list.{position}" convention, which is public contract.
 */
defineProps<{
  /** Module segment of the slot names, e.g. 'users' → 'users.list.toolbar'. */
  module: string
  title: string
  description?: string
  /** Context handed to the extension slots. */
  ctx?: unknown
}>()
</script>
```

Die tatsächliche Schnittstelle von `CalPageHeader` prüfen und `description`/`actions` daran
anpassen.

- [ ] **Schritt 4: Test ausführen und Erfolg prüfen**

- [ ] **Schritt 5: Eine Ansicht umstellen**

In `WebhooksListView.vue` das äußere Gerüst durch `<CalListPage module="webhooks" title="Webhooks">`
ersetzen; `CalPage`/`CalPageHeader`/`CalCard` und den Toolbar-`ExtensionSlot` entfernen.

Eine Ansicht genügt als Beleg; die übrigen vierzehn folgen später ohne Planbedarf.

- [ ] **Schritt 6: Shell-Tests ausführen** — insbesondere `WebhooksListView.test.ts`

- [ ] **Schritt 7: Commit**

---

### Task 7: Vite-Preset und Bibliotheks-Build

**Dateien:**
- Anlegen: `vite-preset.ts`
- Anlegen: `vite.lib.config.ts`
- Anlegen: `src/public/components.ts`, `src/public/tokens.ts`, `src/public/patterns.ts`
- Ändern: `package.json` (Skript `build:lib`)
- Test: `vite-preset.test.ts`

- [ ] **Schritt 1: Den fehlschlagenden Test schreiben**

```ts
import { describe, expect, it } from 'vitest'
import { calloraAdminPlugin } from './vite-preset'

describe('calloraAdminPlugin', () => {
  it('builds one IIFE bundle with fixed names, so the manifest can point at it', () => {
    const config = calloraAdminPlugin({ entry: 'src/main.ts', name: 'CalloraDemoAdminUi' })
    expect(config.build?.lib).toMatchObject({ entry: 'src/main.ts', formats: ['iife'], name: 'CalloraDemoAdminUi' })
    // @ts-expect-error fileName is a function in Vite's lib config
    expect(config.build?.lib?.fileName()).toBe('main.js')
  })

  it('keeps Vue external against the shell global, so a plugin never ships its own Vue', () => {
    const config = calloraAdminPlugin({ entry: 'src/main.ts', name: 'X' })
    const output = config.build?.rollupOptions?.output as { globals: Record<string, string> }
    expect(config.build?.rollupOptions?.external).toEqual(['vue'])
    expect(output.globals.vue).toBe('CalloraAdmin.vue')
  })

  it('outputs to the directory the host publishes for the admin surface', () => {
    expect(calloraAdminPlugin({ entry: 'src/main.ts', name: 'X' }).build?.outDir).toBe('src/Resources/public/admin')
  })

  it('honours an explicit output directory', () => {
    const config = calloraAdminPlugin({ entry: 'src/main.ts', name: 'X', outDir: '../../public/admin' })
    expect(config.build?.outDir).toBe('../../public/admin')
  })

  it('emits a single stylesheet named main.css', () => {
    const config = calloraAdminPlugin({ entry: 'src/main.ts', name: 'X' })
    const output = config.build?.rollupOptions?.output as {
      assetFileNames: (a: { names: string[] }) => string
    }
    expect(config.build?.cssCodeSplit).toBe(false)
    expect(output.assetFileNames({ names: ['style.css'] })).toBe('main.css')
    expect(output.assetFileNames({ names: ['logo.svg'] })).toBe('logo.svg')
  })
})
```

- [ ] **Schritt 2: Test ausführen und Fehlschlag prüfen**

- [ ] **Schritt 3: Preset schreiben**

```ts
import type { UserConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export interface AdminPluginPresetOptions {
  /** Entry module of the plugin's admin UI. */
  entry: string
  /** Global name for the IIFE bundle (must be unique per plugin). */
  name: string
  /** Output directory. Default 'src/Resources/public/admin' — only this is published. */
  outDir?: string
}

/**
 * The blessed Vite config for a Callora admin plugin (source under app/, compiled deliverable
 * under Resources/public — only the deliverable ships). One self-registering IIFE bundle
 * (main.js + main.css, fixed names) with Vue kept EXTERNAL against the shell's global, so the
 * plugin runs inside the shell's single Vue instance instead of shipping its own — two runtimes
 * break reactivity and component instancing across the boundary.
 */
export function calloraAdminPlugin(options: AdminPluginPresetOptions): UserConfig {
  return {
    plugins: [vue()],
    define: { 'process.env.NODE_ENV': '"production"' },
    build: {
      outDir: options.outDir ?? 'src/Resources/public/admin',
      emptyOutDir: true,
      cssCodeSplit: false,
      lib: { entry: options.entry, formats: ['iife'], name: options.name, fileName: () => 'main.js' },
      rollupOptions: {
        external: ['vue'],
        output: {
          globals: { vue: 'CalloraAdmin.vue' },
          assetFileNames: (asset) =>
            asset.names.some((n) => n.endsWith('.css')) ? 'main.css' : (asset.names[0] ?? '[name][extname]'),
        },
      },
    },
  }
}
```

- [ ] **Schritt 4: Bibliotheks-Build anlegen**

`vite.lib.config.ts` — baut `src/public/*` und das Preset nach `dist-lib/`, mit
Deklarationen (`vite-plugin-dts` oder ein separater `vue-tsc`-Lauf). Vue, `vue-router` und
`radix-vue` bleiben external. Umbraco hält dafür `vite.cms.config.ts` neben `vite.config.ts` —
dasselbe Muster.

`package.json`: `"build:lib": "vite build --config vite.lib.config.ts && vue-tsc -p tsconfig.lib.json"`.

Barrel-Dateien anlegen:

```ts
// src/public/components.ts
export { default as CalAlert } from '@/core/ui/CalAlert.vue'
export { default as CalBadge } from '@/core/ui/CalBadge.vue'
// … alle Cal*-Primitive; die Liste gegen `ls src/core/ui/` prüfen

// src/public/tokens.ts
export { CAL_TOKENS, readToken, type CalTokenName } from '@/core/design/tokens'

// src/public/patterns.ts
export { default as CalListPage } from '@/core/patterns/CalListPage.vue'
export { default as ExtensionSlot } from '@/core/extensions/ExtensionSlot.vue'
```

- [ ] **Schritt 5: Tests und beide Builds ausführen**

- [ ] **Schritt 6: Commit**

---

### Task 8: Communication-Bundle auf das Paket umstellen

**Dateien:**
- Ändern: `custom/static-plugins/Communication/src/Resources/app/admin/{package.json,vite.config.ts,src/main.ts}`

Der Beleg, dass der Vertrag trägt.

> **Reihenfolge beachten:** Audit-Finding #116 bringt den Dialer als Vue-Komponente in genau
> dieses Bundle zurück. Diese Task erst ausführen, wenn die Phase-3-Arbeit gemergt ist —
> sonst kollidieren zwei Änderungen in denselben Dateien.

- [ ] **Schritt 1: Abhängigkeit eintragen**

```json
"@callora/admin": "file:../../../../../../../src/Administration/Resources/app/administration"
```

Tiefe gegen die tatsächliche Verzeichnisstruktur prüfen.

- [ ] **Schritt 2: Vite-Konfiguration auf das Preset umstellen**

```ts
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vite'
import { calloraAdminPlugin } from '@callora/admin/vite-preset'

export default defineConfig(
  calloraAdminPlugin({
    entry: fileURLToPath(new URL('./src/main.ts', import.meta.url)),
    name: 'CalloraCommunicationAdminUi',
    outDir: fileURLToPath(new URL('../../public/admin', import.meta.url)),
  }),
)
```

- [ ] **Schritt 3: Registrierung umstellen**

```ts
import { registerPage } from '@callora/admin/extensions'
import CommunicationAdminPage from './CommunicationAdminPage.vue'

// The shell renders this at /extensions/communication. Registration is synchronous at
// top-level so the loader attributes it to this plugin.
registerPage('communication', CommunicationAdminPage)
```

Das abgetippte `CalloraAdminGlobal`-Interface entfällt ersatzlos.

- [ ] **Schritt 4: Bundle bauen und im laufenden System prüfen**

`/admin/extensions/communication` muss unverändert erscheinen.

- [ ] **Schritt 5: Commit**

---

# Phase 2 — `@callora/surface`

### Task 9: `custom/surface-sdk` in die Runtime auflösen

**Dateien:**
- Verschieben: `custom/surface-sdk/src/vite-preset.ts` → `src/Surface.Rendering/Resources/app/surface/src/vite-preset.ts`
- Verschieben: die zugehörige Spec
- Entfernen: `custom/surface-sdk/`
- Ändern: `custom/plugins/SurfaceDemo/package.json` (Pfad)
- Ändern: `.github/workflows/ci.yml`, `.github/dependabot.yml`

Damit verschwindet D1 der Bestandsaufnahme: Der Vertrag wurde bisher zweimal deklariert, weil die
Runtime privat war. Wird die Runtime selbst zum Paket, gibt es nur noch eine Deklaration — nicht
durch einen Test abgesichert, sondern durch Wegfall.

- [ ] **Schritt 1: Preset und Spec verschieben, Importpfade anpassen**

- [ ] **Schritt 2: `custom/surface-sdk` entfernen**

- [ ] **Schritt 3: CI-Job `surface-sdk` entfernen, Dependabot-Eintrag entfernen**

- [ ] **Schritt 4: Tests der Runtime ausführen**

- [ ] **Schritt 5: Commit**

---

### Task 10: Paket-Identität, Lizenz und Bibliotheks-Build

**Dateien:**
- Ändern: `src/Surface.Rendering/Resources/app/surface/package.json`
- Anlegen: `LICENSE`, `src/public/index.ts`, `vite.lib.config.ts`
- Test: `src/public/index.test.ts`

Analog Task 1 und 7, mit den Unterpfaden `./views`, `./context`, `./components`, `./vite-preset`.

- [ ] **Schritt 1–5** wie in Task 1/7, Bezeichner auf `@callora/surface` angepasst.

---

### Task 11: `params` typisieren

**Dateien:**
- Ändern: `src/surface-registry.ts`
- Test: dort ergänzen

Die Runtime übergibt seit jeher beide Props (`mount.ts` rendert
`h(view.component, { context, params })`), aber der Vertrag typisiert die Komponente nur als
`Component` — ein View-Autor hat keine Form, gegen die er annotieren kann.

- [ ] **Schritt 1: Den fehlschlagenden Test schreiben**

```ts
it('exposes the props shape a view author can annotate against', () => {
  const props: SurfaceViewProps = {
    context: {
      workspaceKey: 'acme',
      surfaceKey: 'portal',
      caller: { state: 'guest', subject: { issuer: 'i', subjectId: 's' } },
    },
    params: { leadId: '42' },
  }

  expect(props.params.leadId).toBe('42')
})
```

- [ ] **Schritt 2: Test ausführen und Fehlschlag prüfen**

- [ ] **Schritt 3: Typ ergänzen**

```ts
/**
 * What the runtime hands a view. Both props were always passed; the contract only typed the
 * component as a bare `Component`, so `params` was effectively undocumented.
 */
export interface SurfaceViewProps {
  readonly context: SurfaceContext
  readonly params: SurfaceViewParams
}
```

- [ ] **Schritt 4–5:** Test grün, Commit.

---

### Task 12: SurfaceDemo umstellen

**Dateien:**
- Ändern: `custom/plugins/SurfaceDemo/{package.json,vite.config.ts,src/Resources/app/workspace/src/GreetingPage.vue}`

> **Hinweis:** `custom/plugins/*` sind Beispiel-Plugins und sollen entfernt werden. Diese Task
> nur ausführen, solange SurfaceDemo noch der Referenz-Konsument ist; andernfalls ersatzlos
> streichen und einen neuen Minimal-Konsumenten anlegen.

- [ ] **Schritt 1–4:** Abhängigkeit auf `@callora/surface`, Preset-Import, `SurfaceViewProps`
      annotieren, bauen.

---

### Task 13: Server↔Client-Konsistenztest

**Dateien:**
- Anlegen: `tests/Callora.Core.Tests/Surfaces/SurfaceViewRegistrationConsistencyTests.cs`
- Ggf. ändern: `custom/plugins/SurfaceDemo/src/SurfaceDemoPlugin.cs`

`HostSurfaceViewRegistration.ViewId` (C#) und `registerSurfaceView({ id })` (TS) müssen
übereinstimmen; verbunden sind sie durch nichts als eine Zeichenkette. Ein Tippfehler erzeugt
eine Insel, die nie gefüllt wird — stumm, und erst in Produktion sichtbar. Präzedenz für diese
Art Test ist die Doku-Prüfung aus `69d3195`.

- [ ] **Schritt 1: Den fehlschlagenden Test schreiben**

```csharp
using System.Text.RegularExpressions;

namespace Callora.Core.Tests.Surfaces;

/// <summary>
/// A surface view is declared twice: on the server as a HostSurfaceViewRegistration (which
/// decides visibility, ordering and claims) and in the browser bundle as registerSurfaceView
/// (which supplies the component). The two are joined by nothing but a string.
/// </summary>
public sealed class SurfaceViewRegistrationConsistencyTests
{
    private static readonly Regex ClientRegistration = new(
        @"registerSurfaceView\(\s*\{[^}]*?\bid\s*:\s*['""]([^'""]+)['""]",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ServerRegistration = new(
        @"new\s+HostSurfaceViewRegistration\(\s*['""]([^'""]+)['""]",
        RegexOptions.Compiled | RegexOptions.Singleline);

    [Fact]
    public void EveryClientRegisteredViewHasAServerDeclaration()
    {
        var root = RepositoryRoot();
        var orphans = Scan(root, "*.ts", ClientRegistration).Keys
            .Except(Scan(root, "*.cs", ServerRegistration).Keys, StringComparer.Ordinal)
            .ToArray();

        Assert.True(orphans.Length == 0,
            "Diese Views registrieren sich im Browser, sind aber serverseitig nicht deklariert – " +
            $"sie werden nie in einen Slot eingesetzt: {string.Join(", ", orphans)}");
    }

    [Fact]
    public void EveryServerDeclaredViewHasAClientRegistration()
    {
        var root = RepositoryRoot();
        var orphans = Scan(root, "*.cs", ServerRegistration).Keys
            .Except(Scan(root, "*.ts", ClientRegistration).Keys, StringComparer.Ordinal)
            .ToArray();

        Assert.True(orphans.Length == 0,
            "Diese Views sind serverseitig deklariert, aber kein Bundle registriert eine Komponente – " +
            $"ihre Insel bleibt leer: {string.Join(", ", orphans)}");
    }

    private static Dictionary<string, string> Scan(string root, string pattern, Regex regex)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var directory in new[] { "custom", "src" })
        {
            var path = Path.Combine(root, directory);
            if (!Directory.Exists(path))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(path, pattern, SearchOption.AllDirectories))
            {
                // node_modules and build output carry copies that would double-count.
                if (file.Contains("node_modules", StringComparison.Ordinal) ||
                    file.Contains($"{Path.DirectorySeparatorChar}dist", StringComparison.Ordinal) ||
                    file.Contains($"{Path.DirectorySeparatorChar}public{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                    file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (Match match in regex.Matches(File.ReadAllText(file)))
                {
                    found[match.Groups[1].Value] = file;
                }
            }
        }

        return found;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
```

- [ ] **Schritt 2: Test ausführen** — wahrscheinlich FAIL, weil SurfaceDemo clientseitig
      registriert, aber serverseitig nichts deklariert.

- [ ] **Schritt 3: Den echten Befund beheben**

In `SurfaceDemoPlugin.cs` einen `IHostSurfaceViewContributor` ergänzen und exportieren:

```csharp
using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Plugins.SurfaceDemo;

/// <summary>
/// Declares the demo view on the server, so the slot resolver can place it. The browser bundle
/// registers a component under the same id — the consistency test enforces that pairing.
/// </summary>
public sealed class SurfaceDemoViewContributor : IHostSurfaceViewContributor
{
    public string PluginId => "surface-demo";

    public IReadOnlyList<HostSurfaceViewRegistration> Views { get; } =
    [
        new HostSurfaceViewRegistration(
            ViewId: "surface-demo.greeting",
            Slot: "workspace.main",
            DisplayName: "Begrüßung"),
    ];
}
```

- [ ] **Schritt 4–5:** Test grün, Commit.

---

# Phase 3 — Abschluss

### Task 14: CI, Dependabot, READMEs

- [ ] Job `surface-sdk` entfällt (Task 9); `admin-frontend` und `surface-runtime` bekommen je
      einen `build:lib`-Schritt.
- [ ] Dependabot: `/custom/surface-sdk` entfernen.
- [ ] READMEs für beide Pakete: was es ist, wie man es installiert, ein minimales Beispiel, der
      Vertragsabschnitt. Vorbild: `custom/surface-sdk/README.md`.
- [ ] Root-`LICENSE` klären — sie sagt „All rights reserved", während beide Pakete Apache-2.0
      deklarieren. Das ist eine Entscheidung, keine Aufgabe; hier nur als offener Punkt notiert.

---

## Abschluss

Danach gilt:

- **`@callora/admin`** — typisierte Registrierung gegen einen generierten Katalog, Primitive,
  Muster, ersetzbare Komponenten, Vite-Preset. Das Communication-Bundle ist der erste Konsument
  und hat sein handkopiertes Interface verloren.
- **`@callora/surface`** — Runtime und Vertrag in einem Paket, `params` typisiert, ein Test der
  Server- und Client-Registrierung aneinander bindet.

**Ein Drift ist danach strukturell unmöglich** (Vertrag nur einmal deklariert), **einer wird
maschinell bemerkt** (Katalog-Drift bricht CI), **einer bleibt ein Test** (Server↔Client
überspannt zwei Sprachen).

**Nicht in diesem Plan:** Realtime, Block-Vertrag und Control-Schema, Kompositions-Renderer und
der Composer selbst — Bausteine 4–7 des
[Composer-Designs](../specs/2026-08-06-admin-sdk-und-surface-composer-design.md).
