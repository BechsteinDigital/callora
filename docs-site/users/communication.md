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
- **Call control** — place, answer, reject, send DTMF and hang up, over one
  primitive that the Admin API, MCP tools and Flow actions all share. Whichever
  face is used, the same workspace ownership check, the same state machine and
  the same history writes apply.
- **A live event stream** — a WebSocket carrying the workspace's call
  transitions as they happen, so a dialer lights up while the phone is still
  ringing.
- **Call-control actions for Flows** — `call.accept`, `call.reject`,
  `call.hangup` and `call.dtmf`, so calls can be routed automatically. See
  [Flows](flows.md).

Completed calls are logged to the plugin's own `plugin_communication` database
schema.

**Not implemented:** call recording, and with it recording-consent handling.
Recording needs storage, a retention policy and consent enforcement before it can
be offered, and none of those exist yet — so the plugin does not advertise a
recording capability. There is no separate Dialer plugin either; call control
lives in Communication itself.

## Where it appears

- **Operators** work in the **admin shell** — the plugin's Communication page
  carries the dialer: place a call, answer or reject a ringing one, send DTMF and
  hang up, with the active-call list following the live event stream.
- **Integrations** use the plugin's Admin API under
  `/api/ext/admin/plugins/communication/…` and the same operations as MCP tools.
  Both are workspace-scoped and permission-checked by the host.

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

### Controlling a call

Every call operation is workspace-scoped and reachable three ways — Admin API,
MCP tool, Flow action — over one primitive:

| Operation | Admin API | MCP tool | Flow action |
|---|---|---|---|
| Place | `POST calls` | `place_call` | — |
| Answer | `POST calls/{callId}/accept` | `accept_call` | `call.accept` |
| Reject | `POST calls/{callId}/reject` | `reject_call` | `call.reject` |
| Send DTMF | `POST calls/{callId}/dtmf` | `send_dtmf` | `call.dtmf` |
| Hang up | `POST calls/{callId}/hangup` | `hangup_call` | `call.hangup` |
| List live | `GET calls/active` | `list_active_calls` | — |
| List history | `GET calls` | `list_recent_calls` | — |

Every operation that changes a call needs `communication.calls.manage`; reads
need `communication.calls.read`. One key covers all five control operations
rather than a key per verb — they all act on the same live conversation.

The three answers a control request can get are distinct on purpose. `404` means
the workspace has no such live call. `409` means the call is there but the
request does not apply to its state — answering an outbound call, or one that is
already connected. `400` means the request itself was malformed, for example a
DTMF sequence containing something that is not a keypad tone. A sequence with an
invalid tone is rejected whole, so a bad request never leaves a half-dialled
call.

### Following calls live

```
POST /api/ext/admin/plugins/communication/calls/event-stream
```

returns a single-use ticket and the socket path to redeem it on
(`/ws/communication/calls/{token}`, two-minute window). The socket then carries
one JSON frame per transition:

```json
{
  "eventName": "call.ringing",
  "workspaceKey": "ws-a",
  "callId": "call-1",
  "direction": "Inbound",
  "state": "Ringing",
  "remoteParty": "+49301234567",
  "occurredAt": "2026-08-05T12:00:00+00:00"
}
```

A browser cannot put an Authorization header on a WebSocket handshake, which is
why the stream is reached through a ticket: the permission check happens on the
normal authenticated request that mints it.

The stream is best effort and deliberately so. It is filtered to the workspace,
send-only, and a client that falls too far behind loses its oldest events rather
than slowing the call down — the current picture is always one
`GET calls/active` away. Durable delivery is a different path: `call.*` business
events go through the transactional outbox to flows and webhooks, which survives
a restart but arrives on a job cadence rather than instantly.

### Streaming a live call

An external consumer — a voice agent, a transcription service, a browser
softphone — reaches a call's audio over a WebSocket. It cannot open that socket
on its own: it needs a **one-time ticket**, minted through the Admin API for a
call the caller's workspace is running right now.

```
POST /api/ext/admin/plugins/communication/calls/{callId}/media-streams
{ "consumerRef": "ai-agent", "direction": "bidirectional" }
```

```json
{
  "sessionId": "0198…",
  "callId": "call-1",
  "connectToken": "…",
  "connectPath": "/ws/communication/media/…",
  "direction": "bidirectional",
  "expiresInSeconds": 120,
  "encoding": "audio/x-mulaw",
  "sampleRateHz": 8000
}
```

The ticket needs `communication.calls.manage`, because it hands out live access
to a conversation. Four properties bound what it can do:

| Property | Behaviour |
|---|---|
| Ownership | A call the workspace does not run answers `404` — the same as a call that never existed. |
| Single use | The first connect consumes the token; a second attempt is refused. |
| Expiry | Unredeemed after two minutes, the token is dead. |
| Direction | `inbound` the consumer only listens, `outbound` it only speaks, `bidirectional` both. Enforced on the socket, not just recorded. |

Only the token's hash is stored, so a leaked database row is not a working
ticket. When the call ends, its sessions are closed and its sockets are aborted:
a stream never outlives the conversation it carries, and an unspent ticket for an
ended call stops being redeemable.

### Connecting a browser

A browser needs the same kind of ticket for signalling, plus the ICE
configuration for its `RTCPeerConnection`:

```
POST /api/ext/admin/plugins/communication/webrtc/sessions
{ "target": "browser-1" }
```

```json
{
  "connectToken": "…",
  "connectPath": "/ws/communication/webrtc/…",
  "expiresInSeconds": 120,
  "iceServers": [{ "urls": ["turn:turn.example.com:3478?transport=udp"], "username": "1780…:ws-a", "credential": "…" }],
  "iceCredentialExpiresInSeconds": 600
}
```

The route exists only when the deployment enables WebRTC, and it answers `503`
while Communication is unavailable rather than handing out a ticket that would
fail at connect time.

TURN credentials are derived per session when the deployment configures a
`SharedSecret` for the relay (the TURN REST API scheme that coturn and the
managed services implement), so no long-lived password ever reaches a browser.
Without a shared secret the configured static credentials are passed through
unchanged and `iceCredentialExpiresInSeconds` is absent — the response does not
claim a lifetime the deployment does not keep.

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

### Stopping without cutting people off

Deactivating the plugin or shutting the host down does not hang up whoever is
talking. Communication drains first: it withdraws each SIP registration so the
carrier stops routing here, refuses anything that still arrives with
`503 Service Unavailable` (which sends the call to the next route in the trunk
group rather than giving the caller a busy tone), stops minting WebRTC sessions,
and only then waits for the conversations already in progress to end by
themselves.

The wait is bounded by `CalloraHosting:PluginDrainTimeout`
([configuration](../reference/configuration.md)). While it runs, the status route
answers `draining` — a fourth verdict alongside `ready`, `degraded` and
`unavailable`. **A monitor should treat it as planned, not as an outage:** the
lines it just withdrew report themselves as down, and folding that into
`unavailable` would page someone for an orderly shutdown.

A call that is still up when the deadline expires is ended and recorded as
`interrupted` rather than `failed`. Nothing went wrong with the call; the host
went away underneath it, and a deployment should not leave a history full of
failures behind.

::: tip What a restart still costs
SIP calls do not survive a restart — there is no way to hand a live media session
to a new process, which is why draining exists. Browser-side sessions are a
different story: they can reconnect. See
[ADR-018](https://github.com/BechsteinDigital/callora/blob/main/docs/adr/ADR-018-drain-und-resume-fuer-langlebige-plugins.md)
for where that line runs.
:::

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
