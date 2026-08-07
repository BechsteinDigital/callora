# Exporting Extensions

Extension wiring in Callora is **code-first**. Your `registry.json` declares *who you
are*; the actual "here is my controller / my event listener / my job handler" happens in
code, in one place: your entry class's `StartAsync`. You publish a service by calling
`context.Export(...)`, and the host resolves it back through the read-only
[`ICalloraPluginCatalog`](/api/).

This page is the catalogue of *what* you can export, the export/resolve mechanics, and a
worked `StartAsync` that publishes several contracts at once.

## What you'll learn

- How `context.Export(...)` publishes a service and how the host resolves it back
- The full catalogue of exportable contracts — one row per extension point
- Which host services you **resolve** from the context instead of exporting
- A worked `StartAsync` exporting a job handler, an API controller, flow actions, and a
  purge contributor

## How export and resolution work

The host hands your entry class an [`IHostPluginContext`](./dependency-injection) at
startup. It has exactly two members:

```csharp
public interface IHostPluginContext
{
    // The curated service provider — resolve host services from here.
    IServiceProvider Services { get; }

    // Publish one service instance for a contract type.
    void Export(Type contractType, object service);
}
```

You rarely call the non-generic `Export` directly. A typed helper reads better and null-checks
for you (`HostPluginContextExtensions`):

```csharp
context.Export<IBackgroundJobHandler>(new DialRunJobHandler(executor, numberStore, runStore));
```

On the host side, everything you exported is available through `ICalloraPluginCatalog`:

```csharp
public interface ICalloraPluginCatalog
{
    bool TryGetExport(Type contractType, out object? service);
    IReadOnlyList<object> GetExports(Type contractType);
    IReadOnlyList<CalloraPluginExport> GetOwnedExports(Type contractType);
}
```

- **`GetExports(type)`** — every implementation of a contract across all active plugins.
  This is how the host collects, e.g., *all* `IFlowActionHandler`s.
- **`TryGetExport(type, out service)`** — the single implementation, for one-of contracts.
- **`GetOwnedExports(type)`** — each export paired with its owning `pluginId`, for
  consumers that must gate or attribute a service back to the plugin that supplied it.

Typed helpers (`CalloraPluginCatalogExtensions`) mirror the entry-class side:
`catalog.GetExports<IFlowActionHandler>()` and `catalog.TryGetExport<IThing>(out var thing)`.

::: info
The catalogue is populated from your **active** exports and re-read when plugins activate
or deactivate. You never register into a global container — you export into your plugin's
runtime, and the host reads it. This is what makes hot activate/deactivate possible.
:::

## The catalogue of exportable contracts

Every contract below lives under `src/Core/Application/*/Contracts/`. Export the ones your
plugin implements; the host discovers them by type.

| Contract | What exporting it does | Deeper guide |
| --- | --- | --- |
| `IBusinessEventListener` | React to business events on the host bus (`Priority` + `OnBusinessEventAsync`) | [Events & Jobs](/guides/events-and-jobs) |
| `IServiceDecorator<TService>` | Wrap a host service to change its behaviour (`Order` + `Decorate(inner)`) — the Symfony service-decoration analogue | [Backend Extensions](/guides/backend-extensions) |
| `IApiController` (via `AdminApiController` / `WorkspaceApiController` + `[CalloraRoute]`) | Add HTTP endpoints; the host maps each `[CalloraRoute]`-annotated action | [Backend Extensions](/guides/backend-extensions) · [REST API](/reference/rest-api) |
| `IBackgroundJobHandler` | Handle one durable, at-least-once job type (`JobType` + `ExecuteAsync`) | [Events & Jobs](/guides/events-and-jobs) |
| `IRecurringJobProvider` | Supply fixed-interval recurring job definitions the scheduler enqueues | [Events & Jobs](/guides/events-and-jobs) |
| `IFlowActionHandler` | Add a flow-automation action type (`Type` + `ExecuteAsync`) | [Capabilities](/guides/capabilities) |
| `IRuleConditionEvaluator` | Add a rule/condition type (`Type` + `Evaluate`) | [Capabilities](/guides/capabilities) |
| `IWorkspaceDataPurgeContributor` | Delete your workspace-scoped data on GDPR purge | [Compliance Metadata](./compliance-metadata) |

Two notes on the API base classes: never implement the bare `IApiController` marker
directly. Derive from `AdminApiController` (operator-facing, session + permission) or
`WorkspaceApiController` (workspace-scoped, adds workspace scope). Both inherit
`CalloraApiController`, which gives you response helpers — `Ok(...)`, `Created(...)`,
`NoContent()`, `BadRequest(...)`, `Forbidden()`, `NotFound(...)`, `Conflict(...)`. Routes
are declared per action:

```csharp
[CalloraRoute("GET", "/api/acme/reports", Permission = "acme.reports.read")]
public Task<ApiResult> List(ApiRequest request, CancellationToken cancellationToken) { ... }
```

::: warning
The route template is a real host route. Do **not** shadow a reserved host prefix
(`/api/auth`, `/api/plugins`, `/api/config`, …) — colliding routes are logged and
rejected. Namespace routes under a segment you own (`/api/acme/…`). See
[Best Practices](./best-practices#route-under-a-namespace-you-own).
:::

### Exported vs resolved

Not everything a plugin uses is exported. Some contracts are **host services** you
*resolve* from `context.Services` (or inject into your own types), not publish. Getting
this distinction right is the most common source of confusion.

| Contract | Direction | How you get / give it |
| --- | --- | --- |
| `IBackgroundJobHandler`, `IFlowActionHandler`, `IRuleConditionEvaluator`, `IBusinessEventListener`, `IServiceDecorator<T>`, `IApiController`, `IWorkspaceDataPurgeContributor` | **Exported** by you | `context.Export<T>(instance)` |
| `IPluginDbContextFactory<TContext>` | **Resolved** from host | `context.Services.GetService(typeof(IPluginDbContextFactory<MyDbContext>))` |
| `INotificationPublisher` | **Resolved** from host | resolve, then inject into your handlers |
| `IMailSender` | **Resolved** from host | resolve; sends via configured SMTP |
| `IMediaLibrary` | **Resolved** from host | resolve; read-only workspace media access |
| `ISecretStore` | **Resolved** from host | resolve; named secret lookup |

::: tip
`IPluginDbContextFactory<TContext>` is host-**provided** (you resolve it to build and
migrate your own EF Core `DbContext`), while the `IWorkspaceDataPurgeContributor` that
cleans up that context's data is **exported** by you. One is a tool the host lends you;
the other is a hook you hand back.
:::

Some host services are optional — resolve them defensively and degrade if absent. The
Communication plugin only wires its audio-play flow action when the media library is
available:

```csharp
var mediaLibrary = context.Services.GetService(typeof(IMediaLibrary)) as IMediaLibrary;
if (mediaLibrary is not null)
{
    context.Export<IFlowActionHandler>(new AudioPlayActionHandler(_callHub, mediaLibrary));
}
```

## Worked example: exporting several contracts

Here is the shape of a `StartAsync` that **resolves** host services, then **exports** its
own implementations. The plugin is a dialer: it places calls through Communication's
channel registry and runs each dial run as a durable background job.

```csharp
public sealed class DialerPlugin : IHostManagedPlugin
{
    public const string Id = "acme-dialer";
    public string PluginId => Id;
    public string DisplayName => "Acme Dialer";

    public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 1. Resolve host services from the curated provider.
        var dataStore = ResolveRequired<IPluginDataStore>(context.Services);
        var channelRegistry = ResolveRequired<ICommunicationChannelRegistry>(context.Services);
        var jobQueue = ResolveRequired<IBackgroundJobQueue>(context.Services);

        // 2. Build your own services.
        var numberStore = new DataStoreDialNumberStore(dataStore);
        var runStore = new DataStoreDialRunStore(dataStore);
        var executor = new DialRunExecutor(channelRegistry);
        var coordinator = new DialRunCoordinator(runStore, jobQueue);

        // 3. Export what the host should discover.
        context.Export<IBackgroundJobHandler>(new DialRunJobHandler(executor, numberStore, runStore));
        context.Export<IHostAdminApiExtensionContributor>(
            new DialerAdminApiExtensionContributor(numberStore, coordinator));

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    private static TService ResolveRequired<TService>(IServiceProvider services) where TService : class =>
        services.GetService(typeof(TService)) as TService
        ?? throw new InvalidOperationException($"Host service '{typeof(TService).Name}' is required.");
}
```

A plugin that publishes many kinds at once — controllers, flow actions, an event provider,
and a purge contributor — reads the same way:

```csharp
context.Export<IApiController>(new CallsController(_callHub, channelRegistry));
context.Export<IFlowActionHandler>(new CallAcceptActionHandler(_callHub));
context.Export<IFlowActionHandler>(new CallRejectActionHandler(_callHub));
context.Export<IFlowActionHandler>(new CallHangupActionHandler(_callHub));
context.Export<IWorkspaceDataPurgeContributor>(
    new CommunicationWorkspaceDataPurgeContributor(dbContextFactory));
```

**Expected behavior:** as soon as this plugin activates, the host's catalogue reports the
new job handler under `GetExports<IBackgroundJobHandler>()`, maps the `CallsController`
routes, and includes the three flow actions in `GetExports<IFlowActionHandler>()`. On
deactivation they disappear from the catalogue — no restart, no recompile.

::: tip
Keep `StartAsync` deterministic and cheap: resolve, construct, export, return. Do not do
I/O or block. Long-running or scheduled work belongs in a `IBackgroundJobHandler` or an
`IRecurringJobProvider`, not in startup. See
[Best Practices](./best-practices#deterministic-startup).
:::

## Next steps

- Where these services come from: **[The plugin context & dependency injection](./dependency-injection)**
- HTTP controllers in depth: **[Backend Extensions](/guides/backend-extensions)**
- Jobs & events: **[Events & Jobs](/guides/events-and-jobs)**
- Flow actions & rule conditions: **[Capabilities](/guides/capabilities)**
- Contract signatures: **[.NET contracts reference](/reference/dotnet-contracts)** · **[.NET API](/api/)**
