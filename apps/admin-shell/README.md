# Callora Admin UI

Build-time compiled Admin shell for Host control-plane operations.

## Stack

- Nuxt 3
- Nuxt UI
- TypeScript
- Pinia

## Local usage

```bash
npm install
npm run dev
```

Default dev URL: `http://localhost:3200/admin/`

## Authentication

- Login view is available at `/admin/login`.
- The Admin UI authenticates against `POST /api/auth/login`.
- Backend sets an `HttpOnly` auth cookie on login.
- Frontend does not store access tokens in `localStorage`.
- Session state is validated through `GET /api/auth/me`.
- Registration (`Create Account`) is intentionally not part of the UI.

## Login Extension Hook (for plugins)

The login view exposes a lightweight client-side hook so plugin admin bundles can inject notice blocks:

```ts
window.calloraAdminUi?.registerLoginNoticeExtension?.({
  id: "plugin-id-login-hint",
  position: "before-form",
  title: "Plugin Notice",
  description: "Custom login hint from plugin.",
  color: "info",
  order: 50
});
```

Optional early queue (before bridge initialization):

```ts
window.calloraAdminUi = window.calloraAdminUi || {};
window.calloraAdminUi.queuedLoginNoticeExtensions = [
  ...(window.calloraAdminUi.queuedLoginNoticeExtensions || []),
  {
    id: "plugin-id-login-hint",
    position: "after-form",
    title: "Need support?",
    description: "Contact platform admin."
  }
];
```

## Build output

Run from repository root:

```bash
./scripts/build-admin-ui.sh
```

Default output target:

- `artifacts/admin-shell`

The script also refreshes plugin UI asset discovery via:

- `./scripts/build-plugin-ui-assets.sh`
