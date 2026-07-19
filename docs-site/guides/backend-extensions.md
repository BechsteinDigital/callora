# Backend Extensions

Server-side extension in Callora happens through four mechanisms, all attached in code from
your plugin's `StartAsync` via `context.Export(...)`
(ADR-009). Each is an official
extension point marked `[CalloraExtensible]`.

| Mechanism | Extension point | What it does |
| --- | --- | --- |
| [Business-event listeners](#business-event-listeners) | `IBusinessEventListener` | React to (and optionally cancel) platform activity |
| [Service decoration](#service-decoration) | `IServiceDecorator<TService>` | Wrap a host service to change its behavior |
| [Plugin controllers](#plugin-controllers-and-dynamic-routing) | `IApiController` | Expose HTTP APIs on dynamic routes |
| [Custom EF entities](#custom-ef-entities-and-per-plugin-schemas) | `IPluginDbContextFactory<TContext>` | Own data in an isolated `plugin_<id>` schema |

## Business-event listeners

The business-event bus is the primary way to react to what the platform does. You export
an **`IBusinessEventListener`** (`src/Core/Application/Events/Contracts/`):

```csharp
[CalloraExtensible("Extension point — implement and export to react to business events")]
public interface IBusinessEventListener
{
    int Priority { get; }
    Task OnBusinessEventAsync(IBusinessEvent businessEvent, CancellationToken cancellationToken = default);
}
```

Your listener is invoked for every published event; filter on `businessEvent.EventName`.
The bus (`BusinessEventBus`) orders listeners by **descending `Priority`** — higher runs
first — so host and plugin listeners interleave deterministically.

### Mutable and cancelable events

Events raised *before* an operation commits derive from **`MutableBusinessEvent`**
(`src/Core/Application/Events/`), which extends `InterceptableEvent`. That base gives a
listener three levers:

```csharp
public abstract class InterceptableEvent : IHostEvent, IHostEventPropagationState
{
    public IDictionary<string, object?> State { get; }   // share data between listeners
    public bool IsPropagationStopped { get; }
    public bool IsCanceled { get; }

    public void StopPropagation();  // stop the remaining listeners in this dispatch
    public void Cancel();           // veto the operation the publisher is about to perform
}
```

A listener mutates `State`, calls `StopPropagation()` to short-circuit later listeners, or
calls `Cancel()` to veto — the publisher inspects `IsCanceled` after `PublishAsync` and
decides whether to proceed. See [Events & Jobs](events-and-jobs.md#the-business-event-bus)
for the publish/subscribe cycle and ordering in full.

## Service decoration

To *change* a host service's behavior (rather than merely react), decorate it. The host
opts a service into decoration by marking it `[CalloraExtensible(ExtensionPointMode.Decoratable)]`
and registering it as decoratable; a plugin exports an **`IServiceDecorator<TService>`**
(`src/Core/Application/Extensibility/Contracts/`):

```csharp
public interface IServiceDecorator<TService>
    where TService : class
{
    int Order { get; }                 // lower Order wraps closer to the base service
    TService Decorate(TService inner); // return a wrapper delegating to inner
}
```

Decoration is a **per-call proxy** (`DecoratingServiceProxy<TService>`,
`src/Core/Application/Plugins/`): the proxy resolves the
live decorator chain from the plugin catalog on **every call**, composes it in `Order`, and
invokes it. This is what makes decoration hot-swappable — a decorator from a plugin
activated later takes effect on the next call, and a deactivated plugin's decorator is
dropped immediately, with nothing pinned.

```csharp
public sealed class CallSummaryMailDecorator : IServiceDecorator<IMailSender>
{
    public int Order => 100;
    public IMailSender Decorate(IMailSender inner) => new SummaryWrapper(inner);
}
```

> **Scope:** decoration is deliberately narrow today — it is wired for a small set of
> services marked `Decoratable`, not a general "decorate anything" framework
> (`callora-decoration-dynamic-2026-07`). Security-, compliance-, and lifecycle-critical
> paths are `internal` / `[CalloraInternal]` and are **not** decoratable
> (ADR-013 §4).

## Plugin controllers and dynamic routing

Plugins expose HTTP endpoints without the host recompiling. You export an **`IApiController`**
(`src/Core/Application/Http/Contracts/`) — in practice by deriving from `AdminApiController`
(operator-facing) or `WorkspaceApiController` (workspace-scoped) — and annotate action
methods with **`[CalloraRoute]`**:

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class CalloraRouteAttribute(string httpMethod, string pathTemplate) : Attribute
{
    public string HttpMethod { get; } = httpMethod;   // "GET", "POST", …
    public string PathTemplate { get; } = pathTemplate;
    public string Permission { get; init; } = "";     // required RBAC permission
    public string? Name { get; init; }
}
```

On activation the host reflects over your exported controllers, and
`PluginApiEndpointDataSource` (`src/Core/Infrastructure/Http/`) rebuilds the ASP.NET Core
endpoint table live; on deactivation the routes are removed. The request delegate enforces
authentication, the declared `Permission`, and — for `WorkspaceApiController` — that the
caller has access to the target workspace and that the plugin is available there.

### Reserved route prefixes

A plugin **cannot** register a route under a reserved host prefix
(`ReservedHostRoutePrefixes`, `src/Core/Infrastructure/Http/`). Colliding routes are
rejected at build-time of the endpoint table, so a plugin can never shadow a platform
endpoint. The reserved prefixes are:

```text
/api/auth              /api/config            /api/custom-fields     /api/entitlements
/api/ext/admin         /api/flows             /api/jobs              /api/media
/api/notifications     /api/plugins           /api/security/integrations
/api/security/rbac     /api/tenants           /api/themes            /api/users
/api/webhooks          /api/workspaces        /workspace/auth        /workspace/themes
```

Route your own APIs under a namespace you own (e.g. `/api/voip/...`), not under these.

## Custom EF entities and per-plugin schemas

A plugin owns its data as real EF Core entities in a **dedicated Postgres schema** named
`plugin_<id>`, isolated from the host tables and from other plugins. You define a normal
`DbContext` and pin the schema in `OnModelCreating`. From the Communication plugin
(`custom/static-plugins/Communication/.../VoipDbContext.cs`):

```csharp
public sealed class VoipDbContext(DbContextOptions<VoipDbContext> options) : DbContext(options)
{
    public const string SchemaName = "plugin_communication";

    public DbSet<CallLog> CallLogs => Set<CallLog>();
    public DbSet<SipAccount> SipAccounts => Set<SipAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);   // all tables land in plugin_communication
        // entity configuration …
    }
}
```

The context **must** take `DbContextOptions<TContext>` in its constructor. The host supplies
a connection-bound factory, **`IPluginDbContextFactory<TContext>`**
(`src/Core/Application/Persistence/Contracts/`):

```csharp
public interface IPluginDbContextFactory<TContext>
    where TContext : DbContext
{
    TContext CreateDbContext();
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
```

### Plugin migrations

Ship real EF Core migrations in your plugin assembly. In `StartAsync`, resolve the factory
and call `MigrateAsync` — the host applies pending migrations under a Postgres **advisory
lock** derived from your plugin id, so concurrent hosts cannot race the same schema:

```csharp
var factory = (IPluginDbContextFactory<VoipDbContext>)
    context.Services.GetService(typeof(IPluginDbContextFactory<VoipDbContext>))!;
await factory.MigrateAsync(cancellationToken);
```

Generate migrations the usual way, targeting your plugin project as the migrations
assembly:

```bash
dotnet ef migrations add InitialVoipSchema \
  --project custom/static-plugins/Communication/Callora.Plugin.Communication.csproj \
  --context VoipDbContext
```

> **Workspace purge:** to clean up your workspace-scoped data when a workspace is deleted,
> export an `IWorkspaceDataPurgeContributor` — the host calls it as part of workspace purge.

## Exporting: the common shape

Every backend extension is attached the same way — one `Export` call per implementation in
`StartAsync`:

```csharp
public async ValueTask StartAsync(IHostPluginContext context, CancellationToken ct = default)
{
    context.Export(typeof(IBusinessEventListener), new CallLifecycleListener());
    context.Export(typeof(IServiceDecorator<IMailSender>), new CallSummaryMailDecorator());
    context.Export(typeof(IApiController), new CallsController());
    // …
}
```

The host indexes exports by contract type and resolves them through `ICalloraPluginCatalog`.
Because exports are added on activation and dropped on deactivation, every backend extension
above is **hot** — no restart, nothing to pin.
