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
| **IHostWebSocketEndpointContributor** | Contributable | Contribute host-level WebSocket endpoints (real-time surface). |
| **IHostWebSocketHandler** | Contributable | Service an accepted plugin WebSocket connection. |
| **IWebSocketConnectAuthorizer** | Contributable | Validate a WebSocket connect-token before the connection is accepted. |

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
