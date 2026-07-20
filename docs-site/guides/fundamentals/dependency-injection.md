# Dependency injection & the plugin context

When the host activates your plugin, it hands your `StartAsync` a single object:
`IHostPluginContext`. That context is your entire relationship with the host. It gives you a
`Services` provider to **resolve** host services from, and an `Export` method to **publish**
your own implementations to. There is no constructor injection into a plugin, and no direct
handle to the host's DI container — everything flows through the context.

The catch, and the point of this page: `context.Services` is **not** the host's real
service provider. It's a *curated* view — a filtered surface that exposes published
contracts and cross-plugin exports, and returns `null` for everything else. This is a trust
boundary, and understanding it is what separates a plugin that "just works" from one that
fights the platform.

## What you'll learn

- What `IHostPluginContext` gives you: `Services` and `Export`
- Why `Services` is curated, and exactly which types it will and won't resolve
- The host services you can resolve, and where their contracts live
- How to export your own implementations, and how the host resolves them back
- How this differs from a normal ASP.NET Core DI container — and why

::: tip Prerequisites
Read [the plugin entry class](./plugin-entry) first — `StartAsync` is where you use the
context. Constructor DI does **not** apply to the entry class; the host instantiates it
parameterlessly.
:::

## The context contract

`IHostPluginContext` (`src/Core/Application/Plugins/Contracts/IHostPluginContext.cs`) is
deliberately tiny:

```csharp
public interface IHostPluginContext
{
    /// <summary>Application service provider owned by the host process.</summary>
    IServiceProvider Services { get; }

    /// <summary>Publishes one service instance for the provided contract type.</summary>
    void Export(Type contractType, object service);
}
```

Two members. `Services` is where you **pull** dependencies from; `Export` is where you
**push** your extensions to. That's the whole API surface between a plugin and the host.

## Resolving host services

You resolve host services from `context.Services` inside `StartAsync`. A required dependency
should fail loudly if missing; an optional one you check for `null`:

```csharp
// Required — throw if the host doesn't provide it.
var dataStore = ResolveRequired<IPluginDataStore>(context.Services);

// Optional — degrade gracefully if absent.
var mediaLibrary = context.Services.GetService(typeof(IMediaLibrary)) as IMediaLibrary;
if (mediaLibrary is not null)
{
    context.Export<IFlowActionHandler>(new AudioPlayActionHandler(_callHub, mediaLibrary));
}
```

The host services available to a plugin are the platform's published contracts. The common
ones:

| Service | Contract namespace | What it gives you |
| --- | --- | --- |
| `IMailSender` | `Callora.Core.Application.Mail.Contracts` | Send transactional email |
| `IMediaLibrary` | `Callora.Core.Application.Media.Contracts` | Read/write media assets |
| `INotificationPublisher` | `Callora.Core.Application.Notifications.Contracts` | Push in-app notifications |
| `ISecretStore` | `Callora.Core.Application.Secrets.Contracts` | Read secrets/credentials |
| `IPluginConfigReader` | `Callora.Core.Application.Configuration.Contracts` | Read your plugin's typed configuration |
| `IPluginDataStore` | `Callora.Core.Application.Data.Contracts` | Plugin-bound key/value storage |
| `IPluginDbContextFactory<T>` | `Callora.Core.Application.Persistence.Contracts` | Your own EF context in your `plugin_<id>` schema |

Plus `ILoggerFactory` and `ILogger<T>` for logging.

::: info `IPluginDataStore` is plugin-bound
When you resolve `IPluginDataStore`, the curated provider wraps it in a `PluginBoundDataStore`
keyed by *your* `pluginId`. You physically cannot read or write another plugin's data
through it — the partition is enforced by the platform, not by convention.
:::

## Why `Services` is curated

`context.Services` is a `CuratedPluginServiceProvider`
(`src/Core/Application/Plugins/CuratedPluginServiceProvider.cs`), not the host's root
container. Its `GetService` applies a small set of rules and returns `null` for anything
outside them:

1. **Published contracts** — types whose namespace starts with `Callora.Core.` and ends in
   `.Contracts` (or contains `.Contracts.`). The namespace *is* the boundary: a service in a
   `…Contracts` namespace is public; anything else in Core is a host internal and won't
   resolve.
2. **Contract packages** — types from `Callora.Contracts.*` assemblies, and foundation
   contract packages named `Callora.Plugin.*.Abstractions`.
3. **Logging** — `ILoggerFactory` and `ILogger<T>`.
4. **Plugin-bound storage** — `IPluginDataStore` (wrapped per-plugin) and
   `IPluginDbContextFactory<T>` (your own schema).
5. **Cross-plugin exports** — a contract the host doesn't register itself falls back to
   another plugin's export (see below).

Everything else returns `null`. A plugin **cannot** reach arbitrary host services through
`Services` — no internal repositories, no host-private types, no other plugin's raw
implementation. This is governance and trust, not friction for its own sake: it keeps the
host's internals free to change and prevents plugins from coupling to things they were never
promised.

::: warning `GetService` returns `null`, it does not throw
Because the curated provider returns `null` for disallowed *and* unregistered types, a
`null` can mean "not allowed" or "not provided in this host." Always null-check optional
services, and use a `ResolveRequired<T>` helper for mandatory ones so a missing dependency
fails at activation with a clear message rather than a `NullReferenceException` later.
:::

### Contrast with a normal ASP.NET DI container

| | ASP.NET `IServiceProvider` | Callora curated `Services` |
| --- | --- | --- |
| Scope | Every registered service | Only published contracts + cross-plugin exports |
| Unknown type | Returns `null` | Returns `null` — *and* deliberately excludes host internals |
| Injection point | Constructor DI everywhere | Resolved manually inside `StartAsync` |
| Data access | Shared DbContext / repositories | Plugin-bound store + your own `plugin_<id>` schema |
| Boundary | Assembly / registration | Namespace + package name (a trust boundary) |

If you've written ASP.NET services, the muscle memory to unlearn is "the container has
everything." Here it has *your contract surface* and nothing more.

## Exporting your implementations

`Export` is how your plugin contributes behavior. You publish an instance against the
contract type the host (or another plugin) will resolve it by:

```csharp
// Generic convenience form (recommended):
context.Export<IApiController>(new CallsController(_callHub, channelRegistry));
context.Export<IFlowActionHandler>(new CallAcceptActionHandler(_callHub));
context.Export<IBusinessEventProvider>(new CallBusinessEventProvider());

// A plugin can also export a shared contract for OTHER plugins to resolve:
var channelRegistry = new CommunicationChannelRegistry();
context.Export<ICommunicationChannelRegistry>(channelRegistry);
```

The instance must actually implement the contract — the host validates this and throws if
not:

> Export instance type '…' does not implement 'ICommunicationChannelRegistry'.

Exports are **withdrawn automatically** when your plugin deactivates. You never un-export in
`StopAsync`.

## How the host resolves your exports

Everything you export is indexed by contract type in `RuntimePluginHost` and read back
through `ICalloraPluginCatalog` (`src/Core/Application/Plugins/ICalloraPluginCatalog.cs`):

```csharp
public interface ICalloraPluginCatalog
{
    bool TryGetExport(Type contractType, out object? service);
    IReadOnlyList<object> GetExports(Type contractType);
    IReadOnlyList<CalloraPluginExport> GetOwnedExports(Type contractType);
}
```

- **`TryGetExport`** — one service for a contract. When several plugins export the same
  contract, the **latest active export wins** (highest registration sequence).
- **`GetExports`** — *all* exports for a contract, newest first. This is how the host
  collects many contributors: every plugin's `IApiController`, every `IFlowActionHandler`,
  every `IBusinessEventListener`.
- **`GetOwnedExports`** — the same list, each paired with its owning `pluginId`, for
  consumers that must gate or attribute a contribution by plugin.

Typed convenience wrappers exist in `CalloraPluginCatalogExtensions`
(`TryGetExport<T>`, `GetExports<T>`), so host code rarely touches raw `Type`.

### Cross-plugin exports

There's a subtlety worth knowing. When a plugin resolves a contract from its curated
`Services` and the *host* doesn't register it, the provider falls back to a **cross-plugin
export** (`ResolveExport`) — but **only when exactly one plugin exports that contract**.
That's how the Dialer plugin resolves `ICommunicationChannelRegistry`:

```csharp
// In DialerPlugin.StartAsync — resolved cross-plugin from Communication's export:
var channelRegistry = ResolveRequired<ICommunicationChannelRegistry>(context.Services);
```

Multi-provider contracts (controllers, flow handlers, event providers) are deliberately
*not* resolvable this way through a consuming plugin's `Services` — those are host-collected
via `GetExports` so no single one is picked arbitrarily. Single-provider shared services
(like a channel registry) are; the export is withdrawn automatically if the providing plugin
deactivates.

## A worked example: resolve, then export

Putting both halves together — resolve a host service, then export an implementation that
uses it — as the Communication plugin does:

```csharp
public async ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(context);

    // RESOLVE: a host contract from the curated provider.
    var dataStore     = ResolveRequired<IPluginDataStore>(context.Services);
    var dataProtector = ResolveRequired<IPluginDataProtector>(context.Services);

    // Build the implementation from resolved dependencies.
    var accountStore = new DataStoreSipAccountStore(dataStore, dataProtector);

    // EXPORT: publish a shared contract for other plugins to consume cross-plugin…
    var channelRegistry = new CommunicationChannelRegistry();
    context.Export<ICommunicationChannelRegistry>(channelRegistry);

    // …and publish an admin-API contributor the host will collect via GetExports.
    var channelManager = new SipChannelManager(channelRegistry, new VoipSdkVoiceEngine(), accountStore);
    context.Export<IHostAdminApiExtensionContributor>(
        new VoipAdminApiExtensionContributor(accountStore, channelManager));
}

private static TService ResolveRequired<TService>(IServiceProvider services)
    where TService : class =>
    services.GetService(typeof(TService)) as TService
        ?? throw new InvalidOperationException(
            $"Host service '{typeof(TService).Name}' is required by the plugin.");
```

**Expected behavior:** `IPluginDataStore` resolves (plugin-bound to `communication`);
`IPluginDataProtector` resolves as a published contract; the plugin exports
`ICommunicationChannelRegistry` (which Dialer will later resolve cross-plugin) and an
`IHostAdminApiExtensionContributor` (which the host collects alongside every other plugin's
contributors). If any required host service is absent, activation fails at `StartAsync` with
a named error rather than surfacing later.

## Next steps

- Every export mechanism (controllers, events, decoration, entities): **[Backend Extensions](/guides/backend-extensions)**
- Reading typed settings and secrets: **[Plugin configuration](/guides/fundamentals/plugin-configuration)**
- The lifecycle that drives `StartAsync`: **[The plugin entry class](./plugin-entry)**
- Contract reference: **[.NET API reference](/api/)**
