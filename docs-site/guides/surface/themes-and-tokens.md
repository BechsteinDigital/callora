# Themes & tokens

A surface's look is driven by **design tokens** — CSS custom properties, all named
`--cal-*`. A theme is a plugin that declares those tokens (plus configurable settings) and
publishes CSS that consumes them. This page covers the token cascade, how a theme declares
tokens in `theme.json`, how the resolver produces a workspace's effective values, and how
your surface CSS consumes them.

Grounded in `src/Core/Application/Extensions/WorkspacePublicThemeResolver.cs`,
`WorkspacePublicTheme.cs`, the theme endpoints (`src/Administration/Api/ThemeEndpoints.cs`,
`src/Workspace/Api/WorkspacePublicEndpoints.cs`), the runtime baseline tokens
(`src/Surface.Rendering/Resources/app/surface/src/styles/tokens.scss`), and the
`theme.json` sync service.

## What you'll learn

- The `--cal-*` token convention and the runtime's baseline tokens
- The token cascade (distribution → tenant → workspace → surface) and how it's resolved
- How a theme plugin declares tokens and settings in `theme.json`
- How a workspace's resolved values are exposed at `/workspace/public/theme`
- How surface CSS (Vue components and `.njk` templates) consumes tokens

## The `--cal-*` token convention

Every surface design token is a CSS custom property prefixed `--cal-`. The runtime ships a
small **neutral baseline** so components render sanely before any theme is assigned
(`src/Surface.Rendering/Resources/app/surface/src/styles/tokens.scss`):

```scss
:root {
  --cal-space-4: 1rem;
  --cal-font-sans: system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif;
  --cal-color-fg: #1a1a1a;
  --cal-color-bg: #ffffff;
  --cal-color-muted: #6b7280;
}
```

The baseline is deliberately unopinionated — no branding, no colour scheme. Themes layer
their values on top.

## The cascade

Tokens resolve along four axes, each overriding the one before it:

```text
distribution  →  tenant  →  workspace  →  surface
  (baseline)     (brand)    (per-site)    (per-channel)
   weakest  ─────────────────────────────▶  strongest
```

- **Distribution** — the runtime's baseline `--cal-*` defaults above.
- **Tenant / workspace** — a theme plugin is assigned to a workspace, and the workspace can
  override the theme's default setting values.
- **Surface** — the most specific override, when a workspace exposes several surfaces.

Today the resolved layer that a surface actually consumes is the **workspace** effective
theme: the assigned theme plugin's definition defaults, overridden by that workspace's
stored values.

> **Status:** The resolver implemented today
> (`WorkspacePublicThemeResolver`) merges **theme-definition defaults with
> per-workspace overrides**. The tenant- and surface-level override layers are part of the
> intended cascade but are not yet distinct resolution steps in this resolver — treat
> them as the direction of travel, not shipped behaviour.

### How resolution works

`WorkspacePublicThemeResolver.ResolveAsync(workspaceKey)`
(`src/Core/Application/Extensions/WorkspacePublicThemeResolver.cs`):

1. Loads the workspace; returns `null` if it's inactive, its tenant is inactive, or it has
   no assigned theme plugin/version.
2. Lists the theme's **setting definitions** (with their defaults) and the workspace's
   **override values**.
3. For each active, non-secret setting, takes the workspace override if present, else the
   definition default, and normalises the JSON to a plain string.
4. Returns a `WorkspacePublicTheme` — `{ themePluginId, themeVersion, valuesByKey }`.

::: info Secrets never leak
The public theme is served anonymously, so secret-typed settings are filtered out during
resolution. Only presentable values reach the surface.
:::

## Declaring tokens in `theme.json`

A theme plugin ships a `theme.json` next to its assembly. On install the host reads it via
`ThemeJsonWorkspaceTemplateSyncService` and syncs two things: **template definitions** and
**setting fields** (`config.fields`). Each setting field becomes a token value a workspace
can override.

```json
{
  "surface": "surface",
  "definitions": [
    { "templateKey": "custom-dashboard", "displayName": "Custom Dashboard", "priority": 50 }
  ],
  "config": {
    "fields": {
      "primaryColor": {
        "label": "Primary Color",
        "type": "color",
        "value": "#007bff",
        "required": true,
        "order": 10
      },
      "spaceUnit": {
        "label": "Base Spacing",
        "type": "text",
        "value": "1rem",
        "group": "Layout",
        "order": 20
      }
    }
  }
}
```

Each key under `config.fields` (`primaryColor`, `spaceUnit`) is a **setting key** — the key
you'll read back from the theme API. A field carries `label`, `type` (default `text`, e.g.
`color`/`select`), `value`/`defaultValue`, `required`, `group`/`tab`, `options`, and
`order`. The full schema and its accepted aliases are in
[Extension manifests](/reference/extension-manifests#theme-json-theme-templates-and-setting-tokens).

::: info `theme.json` keys are setting keys, not CSS variable names
`theme.json` defines the *settings* a workspace configures (`primaryColor` → `#007bff`). It
does not itself name a `--cal-*` variable. You bind a setting value to a `--cal-*` property
yourself — see *Consuming tokens* below.
:::

## Section layouts

A **section layout** is the grid a section uses — `single`, `two-2-1`, `sidebar-left` — and the
**regions** inside it are where blocks go. The editor offers exactly what the theme allows, so
nobody can compose a grid nothing can style.

### What the base surface brings

The runtime ships a base set (`single`, `two-1-1`, `two-2-1`, `sidebar-left`, `three-1-1-1`),
styled in the same `tokens.scss` that carries the neutral `--cal-*` values. They are the base,
not the canon: a fresh installation has something to compose with, and a theme replaces or
extends them.

Without them the composition renderer would emit `data-cal-layout` that nothing listens to, and
the editor would offer no choice — which looks like a bug rather than a missing theme.

### Declaring your own

A theme declares which layouts it can render, and which regions exist inside them:

```json
{
  "sectionLayouts": [
    { "key": "single", "label": "One column", "regions": ["main"] },
    {
      "key": "two-2-1",
      "label": "Two columns (2:1)",
      "regions": [
        { "key": "main", "label": "Content" },
        { "key": "aside", "label": "Sidebar" }
      ]
    }
  ]
}
```

A region may be written as a plain string or as `{ key, label }`. The **declared order is the
reading order** — sorting them would put a sidebar before the content it sits next to.

The layout key is what the composition renderer writes into `data-cal-layout`, and the region
key into `data-cal-region`. Your CSS selects on those:

```css
.cal-section[data-cal-layout='two-2-1'] {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: var(--cal-space-4);
}
```

### Inheritance

**Your layouts are added to the base ones**, and one with the same key replaces the base
version entirely (never merges with it — a `two-2-1` carrying the base's regions while your CSS
expects two others would put blocks in regions that do not exist).

Adding is the safe direction: the base stylesheet is always loaded, so the base layouts work
under your theme too, and a theme that only wants to contribute `sidebar-right` need not repeat
the whole palette.

To stand alone with your own grid system:

```json
{ "inheritSectionLayouts": false, "sectionLayouts": [ … ] }
```

Then everything your layouts need is yours to provide. A theme that declares no layouts of its
own inherits regardless — there would otherwise be nothing left to compose with.

::: info Layouts live in the theme, not in the core
This is what keeps the token axis the design authority: nobody can compose a grid the theme
cannot style, and no layout names end up in Callora's contracts. Bring a layout nobody
anticipated and the editor offers it without a change to the platform.
:::

::: warning Dropping a layout does not break a page
If a theme stops declaring a layout — usually because somebody switched themes — sections still
naming it fall back to `single` when rendered. The blocks all stay; the section just becomes one
readable column instead of a grid nothing styles. The editor names the affected sections so the
change is visible rather than mysterious.

A theme that declares **no** layouts at all says nothing about any of them, and nothing falls
back. That is deliberately different from a theme that dropped one.
:::

## Reading a workspace's tokens

The resolved values are exposed anonymously at
`GET /workspace/public/theme?workspaceKey=<key>`
(`src/Workspace/Api/WorkspacePublicEndpoints.cs`):

```json
{
  "workspaceKey": "acme",
  "themePluginId": "acme.brand-theme",
  "themeVersion": "1.0.0",
  "valuesByKey": {
    "primaryColor": "#e4002b",
    "spaceUnit": "1.25rem"
  }
}
```

`valuesByKey` is the workspace's **effective** tokens (defaults + overrides). Operators
inspect and edit them through the authenticated theme API
(`/api/themes/workspaces/{workspaceKey}/settings` and `.../effective` in
`ThemeEndpoints.cs`); the surface just reads the public endpoint.

## Consuming tokens in surface CSS

Your surface CSS — whether a Vue component's `<style>` or a `.njk` template's stylesheet —
consumes tokens with `var(--cal-*)`, always with a fallback so it renders before a theme
loads:

```vue
<style scoped>
.card {
  color: var(--cal-color-fg, #1a1a1a);
  background: var(--cal-color-bg, #fff);
  padding: var(--cal-space-4, 1rem);
  border-color: var(--cal-color-primary, #007bff);
}
</style>
```

To turn a `theme.json` **setting** into a live `--cal-*` property, bind the resolved value
onto `:root` (or a surface wrapper). A common pattern: fetch the public theme and set the
variables, or have your SSR template write them into a `<style>` from the `tokens` context:

```html
<!-- in a .njk template: map resolved setting values onto --cal-* properties -->
<style>
  :root {
    --cal-color-primary: {{ tokens.primaryColor }};
    --cal-space-4: {{ tokens.spaceUnit }};
  }
</style>
```

> **Status:** The wiring that maps a workspace's `valuesByKey` onto `--cal-*` custom
> properties is a **surface-side convention**, not an automatic host injection. The host
> resolves and serves the values (`/workspace/public/theme`) and passes `tokens` into SSR
> templates; binding them to specific `--cal-*` names is done by the theme's own CSS/JS.
> Confirm the exact `tokens` keys your host passes for your surface before relying on
> specific names.

::: tip Name tokens by role, not by value
Prefer `--cal-color-primary` over `--cal-color-blue`. Role-based tokens let a workspace
re-brand by changing one setting value, without every component needing to know the new
colour.
:::

## Next steps

- Consume tokens from an SSR page: **[SSR Templates](./ssr-templates)**
- Consume tokens from a Vue view: **[Building a surface plugin](./building-a-surface-plugin)**
- The full `theme.json` schema: **[Extension manifests](/reference/extension-manifests)**
- The theme endpoints: **[REST API](/reference/rest-api)**
