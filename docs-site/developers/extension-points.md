# Extension Points Reference

This is the **complete, authoritative catalog** of what a Callora plugin may extend. Unlike a
framework where you decorate whatever you find in `vendor/`, Callora's extension surface is
**explicit**: every sanctioned point is marked `[CalloraExtensible]` in the platform source, and the
governance analyzer (CAL0001–0004) keeps you off everything else. This page lists them all; the
narrative guides ([Backend Extensions](../guides/backend-extensions.md),
[Events & Jobs](../guides/events-and-jobs.md), [Capabilities](../guides/capabilities.md)) are the
how-to deep dives.

> This catalog is kept honest by a test (`ExtensionPointCatalogCompletenessTests`): the build fails if
> a `[CalloraExtensible]` type exists in the platform but is not listed here. So the reference can never
> silently fall behind the code.

## How to read the modes

Every extension point carries an `ExtensionPointMode` (`Callora.Core.Extensibility`):

| Mode | What a plugin does | How |
| --- | --- | --- |
| **Contributable** | Adds an implementation **alongside** the host's — additive, no replacement | `implement` + `context.Export<T>(...)` from `StartAsync` |
| **Decoratable** | **Wraps** a host service to change behavior, delegating to the inner for unchanged paths | export `IServiceDecorator<TService>` |
| **Replaceable** | **Replaces** the host implementation under deterministic precedence (non-critical resolvers only) | export a higher-precedence implementation |

Compile against the contracts via the `Callora.Core` NuGet package (`ExcludeAssets="runtime"` — the
host provides them at runtime); SourceLink lets you step into the platform source for the details.

## Plugin entrypoint & code-first registration

| Extension point | Mode | Purpose |
| --- | --- | --- |
| **IHostManagedPlugin** | Contributable | The plugin entrypoint — `StartAsync`/`StopAsync`. Everything below is wired from here. |
| **IDrainablePlugin** | Contributable | Implement alongside the entrypoint when the plugin carries work a stop would cut through — live calls, open sessions. The host asks it to run dry before stopping it, within a deadline it owns ([ADR-018](https://github.com/BechsteinDigital/callora/blob/main/docs/adr/ADR-018-drain-und-resume-fuer-langlebige-plugins.md)). |
| **IHostPluginExtensionContributor** | Contributable | Export code-first registrations (services, options) into the host container. |

## Events

The business-event bus is the primary way to observe and influence platform activity. See
[Events & Jobs](../guides/events-and-jobs.md).

| Extension point | Mode | Purpose |
| --- | --- | --- |
| **IBusinessEvent** | Contributable | Define a plugin business event (the payload). |
| **IBusinessEventProvider** | Contributable | Contribute business events into the bus. |
| **IBusinessEventListener** | Contributable | React to business events (export to subscribe). |
| **IHostEvent** | Contributable | Define a plugin host event. |
| **IHostEventSubscriber** | Contributable | Subscribe to a host event. |
| **MutableBusinessEvent** | Contributable | Base for **cancelable/mutable** *before*-business-events — observe and **veto**. |
| **MutableHostEvent** | Contributable | Base for cancelable/mutable *before*-host-events — observe and veto. |

## HTTP & real-time surfaces

Plugin routes are attached dynamically (no host restart). See
[Backend Extensions → controllers](../guides/backend-extensions.md#plugin-controllers-and-dynamic-routing).

| Extension point | Mode | Purpose |
| --- | --- | --- |
| **IApiController** | Contributable | Expose a plugin HTTP API — implement via `AdminApiController` / `WorkspaceApiController`. |
| **IHostAdminApiExtensionContributor** | Contributable | Contribute Admin-API routes + navigation entries (workspace-scoped via `HostAdminApiRequest.WorkspaceKey`). |
| **IHostAdminApiRouteHandler** | Contributable | Handle one plugin Admin-API route. |

::: warning Admin route scope
`HostAdminApiRouteRegistration.Scope` defaults to `HostAdminApiRouteScope.Workspace`.
The host then resolves the effective workspace — the caller's bound one, or the one a
platform operator names via `?workspaceKey=` — rejects the request with `400` when none
resolves, and dispatches only while your plugin is effectively available there
(entitlement, activation, capabilities, health). Read that workspace from
`HostAdminApiRequest.WorkspaceKey`; **never re-read a workspace from the query**, which
would bypass the gate.

Declare `HostAdminApiRouteScope.Global` only for routes that genuinely touch nothing
workspace-scoped (plugin-wide status, for example). It is an explicit opt-out.
:::
| **IHostWebSocketEndpointContributor** | Contributable | Contribute host-level WebSocket endpoints (real-time surface). |
| **IHostWebSocketHandler** | Contributable | Service an accepted plugin WebSocket connection. |
| **IWebSocketConnectAuthorizer** | Contributable | Validate a WebSocket connect-token before the connection is accepted. Reads `HostWebSocketConnectRequest.Caller` when the upgrade came from a surface. |
| **IHostSessionResumeService** | Host service | Resolve it to make a real-time session resumable. Issue a ticket while the connection is healthy, redeem it in the authorizer when the client comes back ([ADR-018](https://github.com/BechsteinDigital/callora/blob/main/docs/adr/ADR-018-drain-und-resume-fuer-langlebige-plugins.md)). |

::: tip Resume is a promise, not a stored session
The host keeps a token, a deadline, the owning plugin and a payload it never reads. It cannot keep
your session: sockets, SDK peers and negotiated media do not survive a process. On redemption you
get your payload back and **rebuild** the session, which is what a reconnecting client does anyway.

Two consequences worth designing for. Put identity in the payload (which room, which participant,
which role), not state. And hand the token to the client while the connection is healthy: a client
that only learns it when the socket dies has already missed its chance.

Redemption is single use, so issue a fresh ticket on every connect if the client should stay
resumable. Tickets are bound to the issuing plugin and their lifetime is clamped by the host
(`CalloraHosting:SessionResumeMaxLifetime`). The payload is encrypted at rest with a protector whose
purpose carries your plugin id, so a leaked database yields neither a redeemable token nor a readable
payload.
:::

::: warning A resume ticket is a bearer credential — it describes identity, it does not authorize
Whoever holds the token can attempt to redeem it inside its window. The plugin binding stops another
*plugin* from redeeming it; it does not stop another *client* of yours.

So on redemption, check the payload against the caller in front of you. `HostWebSocketConnectRequest.Caller`
carries the surface caller when the upgrade had one — including a recognized guest, who has a stable
subject too (ADR-017 §3). If the seat was issued to a subject, the returning connection must present
the same one:

```csharp
if (payload.SubjectId is { } expected &&
    request.Caller?.Subject.SubjectId != expected)
{
    return WebSocketConnectAuthorization.Deny("resume subject mismatch");
}
```

Where no caller exists at all — an out-of-process agent connecting with a token and nothing else —
bearer is the only model available, and the short window is what bounds it. Say so in your own docs
rather than leaving a reader to assume the ticket authenticated someone.
:::
| **IHostPublicHttpEndpointContributor** | Contributable | Contribute anonymous public HTTP endpoints under `/public/{pluginId}/…` (GET/POST, no platform auth). |
| **IHostPublicHttpRouteHandler** | Contributable | Handle one plugin public HTTP route — responsible for all input validation and access control. |
| **IHostSurfaceIdentityProvider** | Contributable | Authenticate a surface's own visitors (leads, customers, patients). Bound per surface by operator assignment. |
| **IHostSurfaceApiContributor** | Contributable | Contribute HTTP routes a surface's visitors may call, under `/surface-api/{pluginId}/…`. |
| **IHostSurfaceApiRouteHandler** | Contributable | Handle one surface API route — owns the business authorization for the calling subject. |
| **IHostSurfaceViewContributor** | Contributable | Contribute composable views to surface slots; the browser bundle registers the component under the same view id. |
| **ISurfaceContextBroadcaster** | Resolvable | Push a context value to the surfaces a visitor has open, so a server-side event reaches the views that declared they need it. |
| **ISharedContextKeyContributor** | Contributable | Declare shared context keys — anchor, purpose, field visibility, time to live. Declaration is a precondition for publishing. |
| **ISharedContextService** | Resolvable | Publish context that crosses surface boundaries, anchored to a subject or a conversation. |
| **IHostSurfaceDataContributor** | Contributable | Contribute data a server-rendered surface template reads — a product for `/produkt/schuhe`, opening hours for `/kontakt`. |
| **ISurfaceLayoutSource** | Contributable | Supply composed surface layouts. Implemented by the composer plugin; no composer installed means no layout, and a surface renders from `.njk` as before. |

::: warning Everything a data contributor returns reaches the delivered HTML
Whoever fetches the page reads it — on a `Public` surface without signing in. So the
contributor declares whether its data depends on the caller, and the HOST acts on it: a
caller-specific contribution is not invoked on a Public surface at all, and it makes the
response `no-store`, because a proxy in front would otherwise serve the first visitor's data
to everyone after them.

Three outcomes, not two. „Dieses Produkt gibt es nicht" (`SurfaceDataResult.Missing` → 404)
and „ich konnte den Katalog nicht erreichen" (an exception or an overrun budget → 503) are
different answers, and only the contributor can tell them apart. A required contributor that
could only report "did not work" would force the host into a choice where both options are
wrong.

Contributors run at once, each with its own budget, and **must not read each other's data**.
The moment one waits for another, the budget stops being parallel and the failure rules turn
transitive: if A drops out, B goes quietly wrong instead of empty. A contributor that needs
another plugin's data takes that plugin's contract, not its render contribution.
:::

::: tip Two methods, not one with a flag
`GetPublishedAsync` is the only method the public render path calls. `GetDraftAsync` exists
for the editor and requires operator permission at its call site.

The split is the guarantee: there is no `?preview=true`, no header, no parameter with which
a draft could be requested from outside. On a `Public` surface such a hole would sit behind
no authentication at all — and a single method with a boolean would put both behind one
call, making the guarantee a matter of remembering to pass `false`.
:::

::: tip The realtime bridge is one-way
Resolve `ISurfaceContextBroadcaster` and publish under a namespaced, versioned key
(`communication.active-call/v1`); every browser the address covers receives it and the
runtime hands it to the local context channel. A view that declared `RequiresContexts`
updates — no socket, no reconnect, no message format on the plugin's side.

The address decides delivery **on the server**: name a subject and only that visitor's
connections receive the value. There is no client-side filtering to add, because there is
nothing to filter — what a tab does not receive, it cannot read. A browser cannot publish
here at all: everything in a tab is visible to DevTools and to every script on the page, so
a value from there would carry no authority.
:::

::: warning Shared context is personal data, and its contract says so
`ISurfaceContextBroadcaster` reaches the surfaces of one workspace. Crossing a surface
BOUNDARY — an agent desk and the customer's portal on the same call — goes through
`ISharedContextService` and needs a declaration first:

```csharp
new SharedContextKeyDeclaration(
    "communication.active-call/v1",
    SharedContextAnchorType.Conversation,
    Purpose: "Beide Seiten eines laufenden Gesprächs zeigen dessen Zustand an.",
    Fields:
    [
        new("state", SharedContextVisibility.Participant),
        new("customerRecord", SharedContextVisibility.Owner),
    ],
    TimeToLive: TimeSpan.FromMinutes(30),
    PublisherPluginId: "communication")
```

The declaration is what the projection reads: the customer receives `state`, the agent
receives both, and a field nobody declared is not delivered even if the publisher sets it.
Three gates stand between a published value and a browser — the connection holds a matching
anchor, a visible block on that surface declared it needs the key, and the projection leaves
something after cutting what the holder may not see.

Anchors come from the session, never from a request: there is no parameter in which to claim
one. And a key you may not see answers exactly like a key that does not exist — nothing,
never "forbidden", so the set of contexts cannot be enumerated.
:::

::: tip Surface slots ride on Nunjucks inheritance
A view declares the semantic role it fills (`workspace.main`, `lead.detail.panel`), not
the place it occupies. A theme decides where a role appears by calling
`{{ callora_slot('workspace.main') }}` inside one of its own blocks, so `extends`,
`block` and `super()` keep working and a child theme can wrap, move or replace a slot
like any other markup. `callora_view('vc.room')` embeds a single view,
`callora_has_slot(...)` branches on whether anything filled it, and
`callora_slot_views(...)` iterates the resolved views to build your own chrome.

Pass instance parameters at the call site — `{{ callora_slot('lead.detail.panel', { leadId: lead.id }) }}` —
and they reach the Vue component as its `params` prop, so an embedded view can point at
a concrete lead or room instead of deriving everything from the URL.

Ordering, cardinality, surface scoping and `RequiredClaims` are resolved on the server
before any markup exists. A view a visitor may not see is never emitted rather than
hidden in the browser, and the claim match is on presence only: what a claim means stays
with the plugin that issued it.
:::

::: tip Islands collaborate over a versioned context channel
Each island is its own Vue app and stays that way. What two plugins share is a
vocabulary, not an app: a CRM list publishes `crm.lead-selection/v1`, a phone panel and
a video block consume it, and none of the three imports the others.

Deliberately not an event bus. Keys are namespaced and versioned, every publisher
declares itself, and `channel.diagnostics()` answers who publishes what and who is
listening. A key defaults to a single owner; a second claimant is refused and recorded
rather than silently overwriting.

```ts
import { createSurfaceContextScope } from '@callora/surface-sdk'

const scope = createSurfaceContextScope()
const leads = scope.publish({ key: 'crm.lead-selection/v1', publisherPluginId: 'crm' })
scope.subscribe('crm.lead-selection/v1', (lead) => { /* … */ })
onUnmounted(() => scope.dispose())
```

The channel carries UI state, never authority. A value on it arrived from another
script on the same page and proves nothing, so anything that must be enforced still
goes through an authorised surface API route.
:::

::: warning Surface API routes
This is the seam between the two that existed before: the Admin API speaks for an
operator, the public HTTP seam is anonymous, and neither lets you act in the name of an
ordinary CRM, patient or portal user.

The host enforces the surface session, the host binding, plugin availability in that
workspace, the route mount, the body cap, the handler deadline, the same-origin rule and
the audit entry. It does not interpret a single claim. Whether the subject in
`HostSurfaceApiRequest.Caller` may perform the concrete action is yours to decide, and a
handler that skips that check has no authorization at all.

Routes default to `SurfaceApiRouteAudience.Authenticated`. Opt into
`GuestOrAuthenticated` only for state a recognised guest legitimately owns — a cart, a
draft, a multi-step form. The guest subject is a key, not an entitlement.

A route whose template is absolute or contains `..`, and a second route with the same
method and template, are not mounted. The refusal is recorded with its reason and logged
when a request misses, so a route that silently never matches is diagnosable.
:::

::: warning Surface identity providers
The provider receives normalised request metadata plus the values of the credential
sources it declared in `CredentialSources` — never an `HttpContext`, a raw header
collection or the host's own session. Declaring a source is how you ask for it, and the
declaration is what an operator reviews before assigning you.

To become assignable, declare the `surface.identity` capability in your `registry.json`.
The binding itself is operator data on the surface (ADR-017 §5) — a shipped plugin cannot
know a surface key the customer creates later.

The call runs under a hard deadline. A timeout, an exception or an invalid result is a
provider failure, not a fall-through to anonymous: on a protected surface that would be
an access leak, so the surface closes for authenticated access instead.
:::

## Service decoration

Wrap a host service without replacing it — export an `IServiceDecorator<TService>`; the platform
resolves your decorator through a per-call proxy. See
[Backend Extensions → decoration](../guides/backend-extensions.md#service-decoration).

| Extension point | Mode | Purpose |
| --- | --- | --- |
| **IServiceDecorator** | Contributable | The decorator contract you implement + export for any decoratable service. |
| **IMailSender** | Decoratable | Wrap outbound mail (route, template, suppress). |
| **INotificationPublisher** | Decoratable | Route or suppress notifications. |
| **IFeatureFlagService** | Decoratable | Resolve feature flags from an external provider. |
| **IWebhookEventPublisher** | Decoratable | Customize webhook delivery. |

## Background work

Leased jobs with idempotency + fencing. See
[Events & Jobs → jobs](../guides/events-and-jobs.md#the-job-queue).

| Extension point | Mode | Purpose |
| --- | --- | --- |
| **IBackgroundJobHandler** | Contributable | Handle a background job type. |
| **IRecurringJobProvider** | Contributable | Supply recurring jobs (schedules). |

## Automation (rules & flows)

The low-code automation surface — extend the condition/action vocabulary.

| Extension point | Mode | Purpose |
| --- | --- | --- |
| **IRuleConditionEvaluator** | Contributable | Contribute a rule condition. |
| **IFlowActionHandler** | Contributable | Contribute a flow action. |

## Data & compliance

Plugins own their data in an isolated `plugin_<id>` schema. See
[Backend Extensions → data](../guides/backend-extensions.md#custom-ef-entities-and-per-plugin-schemas).

| Extension point | Mode | Purpose |
| --- | --- | --- |
| **IPluginMigration** | Contributable | Define a plugin schema migration. |
| **IWorkspaceDataPurgeContributor** | Contributable | Erase a plugin's workspace data on GDPR purge. |

## Command line

Console commands are callable on the host (e.g. `dotnet <host>.dll <command>`), the same channel
operators use for platform commands.

| Extension point | Mode | Purpose |
| --- | --- | --- |
| **ICalloraConsoleCommand** | Contributable | Contribute a console command (implement + export). |
