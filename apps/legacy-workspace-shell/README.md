# Callora Workspace Shell

Separat deployte Workspace Shell fuer Endnutzer-/Agentenoberflaechen.

## Stack

- Nuxt 4
- TypeScript

## Local usage

```bash
npm install
npm run dev
```

Default dev URL: `http://localhost:3300/`

## Plugin Extension Hook

Workspace-Plugins koennen Info-Banner registrieren:

```js
window.calloraWorkspaceUi?.registerInfoBanner?.({
  id: "plugin-banner-id",
  pluginId: "plugin-id",
  title: "Plugin loaded",
  description: "Workspace extension is active"
});
```

Optional early queue:

```js
window.calloraWorkspaceUi = window.calloraWorkspaceUi || {};
window.calloraWorkspaceUi.queuedInfoBanners = [
  ...(window.calloraWorkspaceUi.queuedInfoBanners || []),
  { id: "queued-banner", title: "Queued banner" }
];
```

## Build output

Run from repository root:

```bash
./scripts/build-workspace-ui.sh
```

Default output target:

- `artifacts/workspace-shell`
