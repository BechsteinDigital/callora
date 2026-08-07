# Workspaces & Surfaces

Callora models a customer estate along three **orthogonal** axes. Getting them
straight is the key to setting the platform up correctly.

## The three axes

| Axis | Question it answers | Analogy |
|---|---|---|
| **Tenant** | Who pays / who owns this | Billing account |
| **Workspace** | Which system + data boundary | An isolated installation |
| **Surface** | Which access channel *and* which page within it | A sales channel, plus its category tree |

They are independent. A tenant can own several workspaces. A single workspace —
one system and one dataset — can expose several surfaces (channels). Put another
way: **shared data means one workspace with N surfaces**; genuinely isolated
systems mean separate workspaces.

- Use one workspace with multiple surfaces when the same data must be reachable
  through different channels (e.g. a public site and an authenticated agent
  desktop over the same records).
- Use multiple workspaces when the systems and data must not mix.

## Workspaces

A **workspace** is the primary system + data boundary. Manage workspaces under
`/workspaces` (API base `/api/workspaces`):

| Action | Endpoint |
|---|---|
| List | `GET /api/workspaces` |
| Get | `GET /api/workspaces/{workspaceKey}` |
| Create / update | `PUT /api/workspaces/{workspaceKey}` |
| Delete | `DELETE /api/workspaces/{workspaceKey}` |

A workspace has a `workspaceKey`, a `displayName`, a `workspaceType`, an active
flag, an optional owning `tenantKey`, and an optional public base URL. It also
carries its resolved public host and path prefix and its assigned theme (see
below).

### Members

Members are users granted a role **within** the workspace; a member with the
`admin` role is that workspace's per-workspace administrator (not a platform
operator). From the workspace detail screen:

| Action | Endpoint |
|---|---|
| List members | `GET /api/workspaces/{workspaceKey}/members` |
| Add / change role | `PUT /api/workspaces/{workspaceKey}/members/{userId}` |
| Remove | `DELETE /api/workspaces/{workspaceKey}/members/{userId}` |

The member list is cursor-paginated (`limit` and `cursor` query parameters),
ordered by user id, so large workspaces page cleanly.

## Surfaces

Surfaces form a **tree** (ADR-019). A node without a parent is an **application root** — what
you would call a sales channel: a website, a dialer, an agent desktop. It carries the access
itself. A node *with* a parent is a **page**: it inherits the access and overrides only what it
needs, the way a Shopware category sits inside a sales channel.

```text
Contact Center            ← root: host, access mode, theme, login
├── Arbeitsplatz          ← page: inherits all of it
├── Kunden
│   └── Detail
└── Auswertungen
```

**Every node can carry a layout** — that is the point of the tree. Before it, there was exactly
one layout per surface, so a website with three pages would have needed three access channels.

Surfaces live under a workspace (API base `/api/workspaces/{workspaceKey}/surfaces`):

| Action | Endpoint |
|---|---|
| List | `GET /api/workspaces/{workspaceKey}/surfaces` |
| Get | `GET /api/workspaces/{workspaceKey}/surfaces/{surfaceKey}` |
| Create / update | `PUT /api/workspaces/{workspaceKey}/surfaces/{surfaceKey}` |
| Delete | `DELETE /api/workspaces/{workspaceKey}/surfaces/{surfaceKey}` |

Each surface defines:

- **Its place in the tree** — `parentSurfaceKey` (empty for an application root) and `position`
  among siblings, which is the order the navigation shows.
- **Public routing** — an optional `publicHost` and a `publicPathPrefix` (plus an optional
  `publicBaseUrl`). On a child, the path prefix is **its own segment only** (`partner`, not
  `/portal/partner`): the full path is composed from the chain, so moving a subtree does not
  require rewriting every descendant.
- **Access mode** — one of:

  | Mode | Meaning |
  |---|---|
  | `Public` | Reachable without authentication (e.g. a public website) |
  | `Authenticated` | Requires a valid login (e.g. a dialer or agent desktop) |
  | `Mixed` | Both public and protected routes (e.g. a site with a customer area) |

- Optional **locale**, an optional **template** (`templatePluginId` /
  `templateVersion`) and an optional **theme** (`themePluginId` /
  `themeVersion`), and an active flag.

- **Who may see it** — `requiredClaims`, comma-separated. A visitor without them gets a 404,
  not a 403: a node they may not see behaves like one that does not exist. Requirements are
  **cumulative down the tree** — what a parent demands also holds for its children, because a
  child has its own URL and could otherwise be reached around the protection.

  This is deliberately unlike the access mode, which a child may override in *both* directions:
  a public imprint under an authenticated portal is as legitimate as a protected partner area
  under an open website.

  > These are the **visitor's** claims from their surface identity — not the operator RBAC. A
  > portal visitor is not an operator and has no backend role.

Inheritance runs up to the next **root** and no further. Two nodes under one root belong to the
same application; two under different roots do not, even in the same workspace. That is also
where a login ends: only a root assigns an identity provider, so signing in at one covers its
whole tree and nothing beyond it.

Because surfaces hang off one workspace, several application roots can present the same
workspace data through different channels and access modes at once.

## Themes and branding

Branding is a **token axis**: themes are contributed by plugins per *surface*
(for example a `surface` surface or the `admin` surface), versioned, and can
inherit from a parent theme so a child overrides only what it needs. You assign a
theme to a workspace and then tune its settings.

Theme administration lives under `/themes` (API base `/api/themes`):

- **Definitions** — `GET /api/themes/definitions` lists available themes;
  operators register and activate versions under
  `/api/themes/definitions/{templateKey}/plugins/{pluginId}/versions/{version}`.
- **Per-workspace assignment** —
  `GET|PUT|DELETE /api/themes/workspaces/{workspaceKey}` assigns, reads, or
  clears the theme for a workspace; the assignment records who set it and when.
  `GET /api/themes/workspaces/{workspaceKey}/effective` resolves what actually
  applies after inheritance.
- **Per-workspace settings** —
  `GET|PUT /api/themes/workspaces/{workspaceKey}/settings` reads and writes the
  theme's configurable fields (colors, fonts, and so on). Each field carries a
  type, label, group, and default; only active fields are editable. Setting or
  assignment changes invalidate the workspace's theme cache.

The workspace UI itself resolves its effective theme through
`GET /workspace/themes/effective?workspaceKey=...`.

Next: [Communication (VoIP)](communication.md).
