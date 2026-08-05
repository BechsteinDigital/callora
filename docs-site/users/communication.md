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

### Supported SIP authentication

Only **digest** authentication (a registering account or a credentialed,
registering trunk) can be connected. That covers the mass market — sipgate,
easybell, Telekom CompanyFlex and comparable trunks all offer a registering
variant.

Two methods are **refused with `422`** rather than advertised, because the voice
provider cannot operate them yet:

| Method | Why it is refused | Tracked as |
|---|---|---|
| IP-authenticated trunk | The provider always registers; there is no registration-less mode. | [callora-voip-sdk#104](https://github.com/BechsteinDigital/callora-voip-sdk/issues/104) |
| Mutual TLS | The provider's TLS configuration is per client, not per account, and loads its certificate from a file rather than the secret store. | [callora-voip-sdk#183](https://github.com/BechsteinDigital/callora-voip-sdk/issues/183) |

For a carrier that offers only mutual TLS, use digest over a `Tls` transport —
the signalling is still encrypted, only the client certificate is unavailable.

An account of an unsupported kind created before this refusal existed stays in
the database and is reported as **failed** with that reason on startup, instead
of sitting on `Connecting` forever.

### Runtime capabilities

The plugin provides `communication.foundation` unconditionally, and three capabilities only
while a channel that can serve them is registered and healthy:

| Capability | Published by | Healthy when |
|---|---|---|
| `communication.voice` | SIP channel, WebRTC channel | The account is registered / the deployment is reachable |
| `communication.webrtc` | WebRTC channel | STUN/TURN is configured or the bind address is routable |
| `communication.video` | Conference channel | Same reachability as WebRTC |

A dependent plugin declaring one of these in `requiresCapabilities` activates once it is
granted, and is gated again when the channel behind it goes unhealthy or is deregistered.

The WebRTC and conference channels are provisioned per workspace the first time that
workspace's WebRTC surface is used, so their capabilities appear then rather than at plugin
start.

### Account status and readiness

Each account carries the state the voice provider last reported, so the admin
list distinguishes a deliberate choice from a fault:

| Status | Meaning |
|---|---|
| `Disabled` | Switched off by an operator. Not a fault. |
| `Connecting` | Provisioned, no registration reported yet. |
| `Up` | Registered; calls can be placed and received. |
| `Degraded` | Impaired but still carrying calls. |
| `Failed` | Not registered. `lastError` says why. |

`lastRegisteredAt` keeps the moment of the last successful registration even
after a failure, so "never worked" and "worked until an hour ago" are
distinguishable. `lastError` is redacted before it is stored: a provider message
that quotes `sip:user:password@host` or an `Authorization` header is stripped of
the credential and truncated.

`GET /api/ext/admin/plugins/communication/status` aggregates the dependencies
that gate a call (`database`, `channels`, `sip`, `webrtc`) and answers `200`
while calls are possible, `503` when they are not. A dependency the deployment
does not use reports `not-configured` and never drags the verdict down, so a
voice-only install is `ready` without WebRTC. This is readiness only. Host
liveness stays separate, so a carrier outage never gets a healthy process
restarted.

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
