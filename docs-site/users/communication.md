# Communication (VoIP)

Communication is Callora's flagship plugin — the beachhead product. It adds
voice/telephony to a workspace: SIP channels, live calls, a browser dialer, and
call events that feed automation. This page describes it from an operator's and
end user's point of view.

## What it does

The Communication plugin (`custom/static-plugins/Communication`) is a
**system-tier** plugin — `pluginId: communication`, capability
`communication.voice`. It provides:

- **SIP channels** — one SIP account maps to one voice channel per workspace.
  Channels are registered per workspace and expose the `communication.voice`
  capability that other plugins build on.
- **Live calls** — a call moves through `Connecting`, `Ringing`, `Connected`,
  and `Terminated`, and emits business events (`call.ringing`, `call.placed`,
  `call.state-changed`, `call.ended`) onto the platform event bus.
- **A workspace dialer** — a browser UI to place outbound calls (optionally
  choosing a channel), answer or reject incoming calls, send DTMF digits, and
  hang up, with call state streamed live.
- **Call-control actions for Flows** — accept, reject, hang up, and (where a
  media library is available) play audio, so calls can be routed automatically.
  See [Flows](flows.md).
- **Recording-consent handling** — an optional per-call consent flow
  (`NotRequested` / `Pending` / `Granted` / `Denied`), typically driven by DTMF,
  to support recording-consent requirements.

Completed calls are logged to the plugin's own `plugin_communication` database
schema.

### The Dialer plugin

A separate, dynamically installable **Dialer** plugin
(`custom/plugins/Dialer`, `pluginId: dialer`) builds on Communication: it
declares `requiresCapabilities: ["communication.voice"]` and uses the channel
registry to dial workspace numbers. It only works where the Communication plugin
is installed and active.

## Where it appears

- **End users** work in the **workspace surface** — the calls page (dialer,
  incoming-call accept/reject, active-call list with DTMF and hangup, live event
  stream). This is served through a workspace surface, so it needs an
  `Authenticated` (or `Mixed`) surface with a signed-in user. See
  [Workspaces & Surfaces](workspaces-surfaces.md).
- **Calls API** — the plugin exposes `/api/calls` (and a live events stream) for
  the workspace UI and integrations.

## Enabling it per workspace

Voice is turned on for a workspace by two operator steps:

1. **Install and activate** the plugin (see
   [plugin management](administration.md#plugin-management)). Communication is a
   system-tier plugin, so in production it must be signed and its signer trusted.
2. **Entitle** the workspace to it. Entitlements gate plugin access per scope —
   platform-wide, per tenant, or per workspace:

   | Action | Endpoint |
   |---|---|
   | List entitlements | `GET /api/entitlements` |
   | Grant / revoke | `PUT /api/entitlements` |

   Set `pluginId` with a `workspaceKey` (workspace scope), a `tenantKey` (whole
   tenant), or neither (platform-wide), plus `isEntitled`. Entitlements can also
   arrive from a marketplace sync (`POST /api/entitlements/sync`); the Entitlements
   screen (`/entitlements`) shows the current grants and their source.

Then configure the workspace's SIP account(s) so the channel can connect, and
your users can place and receive calls from the workspace dialer.

## Current scope — an honest note

The call stack, dialer UI, call events, consent handling, and Flow call-control
actions are real, working code, not scaffolding.

> **Status:** The Communication plugin declares a **SIP Accounts** admin
> navigation item, but the admin-shell forms for managing SIP accounts are not
> yet implemented. SIP accounts are managed through the plugin's admin API
> (`/api/.../sip-accounts`) in the meantime. Also, SIP connectivity depends on a
> configured external voice/SIP backend — Callora provides the channel and call
> orchestration, not the carrier.

Next: [Flows](flows.md).
