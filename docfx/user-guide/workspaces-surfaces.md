# Workspaces & Surfaces

Callora models a customer estate along three **orthogonal** axes. Getting them
straight is the key to setting the platform up correctly.

## The three axes

| Axis | Question it answers | Analogy |
|---|---|---|
| **Tenant** | Who pays / who owns this | Billing account |
| **Workspace** | Which system + data boundary | An isolated installation |
| **Surface** | Which access channel | A sales channel |

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

A **surface** is an access channel into a workspace — roughly a sales channel.
Surfaces live under a workspace (API base
`/api/workspaces/{workspaceKey}/surfaces`):

| Action | Endpoint |
|---|---|
| List | `GET /api/workspaces/{workspaceKey}/surfaces` |
| Get | `GET /api/workspaces/{workspaceKey}/surfaces/{surfaceKey}` |
| Create / update | `PUT /api/workspaces/{workspaceKey}/surfaces/{surfaceKey}` |
| Delete | `DELETE /api/workspaces/{workspaceKey}/surfaces/{surfaceKey}` |

Each surface defines:

- **Public routing** — an optional `publicHost` and a `publicPathPrefix` (plus an
  optional `publicBaseUrl`) that place the surface at a URL.
- **Access mode** — one of:

  | Mode | Meaning |
  |---|---|
  | `Public` | Reachable without authentication (e.g. a public website) |
  | `Authenticated` | Requires a valid login (e.g. a dialer or agent desktop) |
  | `Mixed` | Both public and protected routes (e.g. a site with a customer area) |

- Optional **locale**, an optional **template** (`templatePluginId` /
  `templateVersion`) and an optional **theme** (`themePluginId` /
  `themeVersion`), and an active flag.

Because surfaces hang off one workspace, several surfaces can present the same
workspace data through different channels and access modes at once.

## Themes and branding

Branding is a **token axis**: themes are contributed by plugins per *surface*
(for example a `workspace` surface or the `admin` surface), versioned, and can
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
