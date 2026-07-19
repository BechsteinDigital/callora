# Getting Started

This page walks a new operator from a running instance to signed in and looking
at the admin shell.

## How Callora is started and reached

Callora is a framework of packable libraries; the runnable process is assembled
by the separate `callora-production` distribution (one app container plus
Postgres). For local work the repository ships Docker Compose files:

```bash
docker compose -f docker-compose.dev.yml up -d
```

The backend then answers a liveness probe at `http://localhost:5000/health`.
Configuration keys use the ASP.NET double-underscore convention, e.g.
`BackendHost__DatabaseConnectionString`. Copy `.env.example` to `.env` and fill
it in before starting.

The admin shell is a Vue 3 single-page app shipped inside the Administration
package and served at **`/admin`** by the host. Open `/admin` in a browser to
reach the sign-in screen.

## First operator: the bootstrap account

On a fresh database (empty `BackendUsers` table) the host can seed a single
**initial operator** so you can sign in without touching the database directly.
It is configured under `BackendHost__InitialOperator`:

| Key | Purpose |
|---|---|
| `BackendHost__InitialOperator__Enabled` | Turn bootstrap seeding on/off |
| `BackendHost__InitialOperator__ExternalId` | Login id (default `admin`) |
| `BackendHost__InitialOperator__Email` | Optional email |
| `BackendHost__InitialOperator__DisplayName` | Optional display name |
| `BackendHost__InitialOperator__Password` | The initial password |

The seeded account is granted the global `superadmin` role.

### Password policy

The initial-operator password must be at least **12 characters**. If it is
shorter, seeding is refused and a warning is logged rather than creating a weak
operator. While bootstrap seeding is enabled, the host also logs a reminder on
every start: after the first sign-in, change the password, set
`BackendHost__InitialOperator__Enabled=false`, and remove the credentials from
your `.env`.

Seeding only runs when there are no users yet, and it never overwrites an
existing account — so a password you change in the UI survives restarts.

> The bootstrap operator is for standing up an instance. For everyday accounts,
> create users in the admin shell (see [Administration](administration.md)).

## Signing in

The sign-in screen (`/admin/login`) posts to `POST /api/auth/login` with your
login, password, and — for a workspace admin — a workspace key. A successful
login returns a bearer token (valid for one hour) plus your display name, email,
and role. The shell then loads your admin context and routes you to the
dashboard.

- **Operators** (holders of the global `superadmin` role) sign in without a
  workspace and get platform scope: access across all workspaces.
- **Workspace admins** sign in against a specific workspace and are locked to it.

See [Administration](administration.md) for how scope is expressed and enforced.

## The admin shell layout

The shell is a fixed sidebar plus a top bar:

- **Sidebar** — the brand and the navigation. The visible entries mirror your
  permissions (a super admin sees all of them):

  | Label | Route | English gloss |
  |---|---|---|
  | Übersicht | `/` | Dashboard |
  | Benutzer | `/users` | Users |
  | Rollen | `/roles` | Roles |
  | Workspaces | `/workspaces` | Workspaces |
  | Mandanten | `/tenants` | Tenants |
  | Plugins | `/plugins` | Plugins |
  | Berechtigungen | `/entitlements` | Entitlements |
  | Medien | `/media` | Media |
  | Flows | `/flows` | Flows |
  | Themes | `/themes` | Themes |
  | Jobs | `/jobs` | Background jobs |
  | Webhooks | `/webhooks` | Webhooks |
  | Konfiguration | `/config` | System configuration |

  Hiding a link is a convenience only — the server remains authoritative, so the
  API refuses a call your role is not permitted regardless of what the sidebar
  shows.

- **Top bar** — the **workspace switcher** and your user menu.

- **Content** — the **dashboard** (Übersicht) opens first. It shows at-a-glance
  KPIs — user count, workspace count, active plugin count, and current jobs
  (each gated by the matching read permission) — plus an identity card with your
  scope, roles, operator flag, and permission count. Plugins can add their own
  dashboard tiles.

### The workspace switcher

The switcher only appears for **operators**. It lists the workspaces you can
reach and remembers your selection in the browser, so plugin views and
workspace-scoped screens act on the workspace you picked. A **workspace admin**
sees no switcher — their session is fixed to a single workspace by the token.

Next: [Administration](administration.md).
