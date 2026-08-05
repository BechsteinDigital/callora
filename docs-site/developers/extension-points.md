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
| **IHostPublicHttpEndpointContributor** | Contributable | Contribute anonymous public HTTP endpoints under `/public/{pluginId}/…` (GET/POST, no platform auth). |
| **IHostPublicHttpRouteHandler** | Contributable | Handle one plugin public HTTP route — responsible for all input validation and access control. |
| **IHostSurfaceIdentityProvider** | Contributable | Authenticate a surface's own visitors (leads, customers, patients). Bound per surface by operator assignment. |
| **IHostSurfaceApiContributor** | Contributable | Contribute HTTP routes a surface's visitors may call, under `/surface-api/{pluginId}/…`. |
| **IHostSurfaceApiRouteHandler** | Contributable | Handle one surface API route — owns the business authorization for the calling subject. |

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
