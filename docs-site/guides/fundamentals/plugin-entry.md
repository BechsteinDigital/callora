# The plugin entry class

Every Callora plugin has exactly one **entry class**: a type implementing
`IHostManagedPlugin`. It's the only thing the host instantiates directly. The host discovers
it (by the `entryTypeName` in your `registry.json`, or by scanning for the interface), calls
`StartAsync` when the plugin activates, and `StopAsync` when it deactivates. Nothing else in
your plugin runs until `StartAsync` wires it up.

## What you'll learn

- The four members of `IHostManagedPlugin` and what each is for
- What belongs in `StartAsync` (resolve, export, migrate) versus `StopAsync` (release)
- How the activate/deactivate lifecycle drives `StartAsync`/`StopAsync`
- What the `[CalloraExtensible]` marker on the contract means
- A complete, annotated entry class from the first-party Communication plugin

::: tip Prerequisites
You should have built and run a plugin once — see
[Build your first Callora plugin](/guides/getting-started/your-first-plugin). You'll also
want a passing familiarity with the [plugin context](./dependency-injection), since
`StartAsync` receives one.
:::

## The contract

The entry contract lives in
`src/Core/Domain/Plugins/Contracts/IHostManagedPlugin.cs`:

```csharp
[CalloraExtensible("Plugin entrypoint — implement to provide a runtime-loadable plugin (REV2 §8.2)")]
public interface IHostManagedPlugin
{
    /// <summary>Stable plugin identifier.</summary>
    string PluginId { get; }

    /// <summary>Display name shown by host tooling.</summary>
    string DisplayName { get; }

    /// <summary>Starts the plugin and registers runtime exports.</summary>
    ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default);

    /// <summary>Stops the plugin and releases runtime resources.</summary>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
```

Four members, no more:

| Member | Purpose |
| --- | --- |
| `PluginId` | Stable identifier. Must match `pluginId` in `registry.json` — the host rejects activation on mismatch. |
| `DisplayName` | Human-readable name shown by host tooling and the admin UI. |
| `StartAsync(context, ct)` | Called on activation. Resolve host services, export your extensions, run migrations. |
| `StopAsync(ct)` | Called on deactivation. Release everything `StartAsync` acquired. |

::: warning `PluginId` must match the manifest
`RuntimePluginHost` compares the running plugin's `PluginId` against the installed record
and **fails activation** if they differ:

> Plugin id mismatch. Expected 'communication', but plugin returned '…'.

The idiomatic pattern is a `public const string Id` on the class that both the property and
your `registry.json` reference, so the two can't drift.
:::

## The lifecycle: activate calls Start, deactivate calls Stop

The entry class is a *lifecycle* hook, not a constructor. The host's `RuntimePluginHost`
drives it:

- **Activate** → the host loads your assembly into a collectible load context, creates the
  entry class with `Activator.CreateInstance` (so **your entry class needs a parameterless
  constructor** — no constructor DI here), builds an `IHostPluginContext`, and awaits
  `StartAsync`. On success the plugin is `Active`; if `StartAsync` throws, activation is
  reported `Faulted` and any exports you already registered are rolled back.
- **Deactivate** → the host removes your exports, awaits `StopAsync`, then unloads the
  assembly load context. If `StopAsync` throws or the context stays pinned, the plugin is
  reported `UnloadFailed` and a host restart is required to fully release it.

This maps to the lifecycle action codes in `PluginLifecycleActions`
(`plugin.activate`, `plugin.deactivate`, …). Activation and deactivation are **hot** — no
host restart — which is exactly why clean teardown in `StopAsync` matters: a leaked timer,
subscription, or handle can pin the load context and block unload.

::: info Instantiation, not injection
Because the host calls `Activator.CreateInstance(pluginType)`, the entry class is
constructed with no arguments. You get your dependencies *inside* `StartAsync` from
`context.Services` — never through the entry class constructor. See
[dependency injection](./dependency-injection).
:::

## What belongs in `StartAsync`

`StartAsync` is where your plugin comes alive. Three kinds of work belong here.

**1. Resolve the host services you need.** Pull them from `context.Services` (the curated
provider). A small `ResolveRequired<T>` helper makes required dependencies fail loudly:

```csharp
private static TService ResolveRequired<TService>(IServiceProvider services)
    where TService : class
{
    return services.GetService(typeof(TService)) as TService
        ?? throw new InvalidOperationException(
            $"Host service '{typeof(TService).Name}' is required by the plugin.");
}
```

**2. Run migrations for your own data.** If your plugin owns an EF context, migrate it
*first*, before anything reads it. The Communication plugin does exactly this:

```csharp
await dbContextFactory.MigrateAsync(cancellationToken).ConfigureAwait(false);
```

**3. Export your extensions.** Publish every controller, event listener, flow action, or
shared service via `context.Export(...)`. This is the code-first wiring — it's what makes
your plugin *do* something. The host indexes each export and resolves it back through
[`ICalloraPluginCatalog`](./dependency-injection#how-the-host-resolves-your-exports).

## What belongs in `StopAsync`

`StopAsync` releases what `StartAsync` acquired, in reverse order. You do **not** need to
un-export your services — the host removes your exports automatically on deactivation.
What you *do* own is everything the host can't see: event subscriptions, background loops,
open connections, `IDisposable`/`IAsyncDisposable` resources.

::: warning Leaked resources block hot unload
Callora unloads plugins from collectible assembly load contexts. Anything you leave running
— an attached event handler, an undisposed hub, a live SDK engine — keeps the context
pinned and turns a clean deactivation into an `UnloadFailed` that needs a host restart.
Detach and dispose everything.
:::

## A complete worked entry class

Here is the shape of a real entry class, condensed from
`custom/static-plugins/Communication/src/CommunicationPlugin.cs`. Note how `StartAsync`
resolves, migrates, and exports, and how `StopAsync` tears down in reverse.

```csharp
public sealed class CommunicationPlugin : IHostManagedPlugin
{
    public const string Id = "communication";

    private SipChannelManager? _channelManager;
    private IVoiceEngine? _engine;
    private VoipCallHub? _callHub;
    private VoipCallBusinessEventRelay? _businessEventRelay;

    public string PluginId => Id;
    public string DisplayName => "Callora Communication";

    public async ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // 1. Resolve host services from the curated provider.
        var dataStore     = ResolveRequired<IPluginDataStore>(context.Services);
        var dataProtector = ResolveRequired<IPluginDataProtector>(context.Services);

        // 2. This plugin OWNS a shared contract and exports it so other plugins
        //    (e.g. Dialer) can resolve it cross-plugin — the host stays unaware.
        var channelRegistry = new CommunicationChannelRegistry();
        context.Export<ICommunicationChannelRegistry>(channelRegistry);

        // 3. Own EF database: migrate first, then use it.
        var dbContextFactory = context.Services.GetService(typeof(IPluginDbContextFactory<VoipDbContext>))
            as IPluginDbContextFactory<VoipDbContext>;
        if (dbContextFactory is not null)
        {
            await dbContextFactory.MigrateAsync(cancellationToken).ConfigureAwait(false);
        }

        // 4. Export the plugin's runtime surface: hubs, controllers, flow actions…
        _callHub = new VoipCallHub(channelRegistry);
        _callHub.AttachToChannels();
        context.Export<ICallDirectory>(_callHub);
        context.Export<IApiController>(new CallsController(_callHub, channelRegistry));
        context.Export<IFlowActionHandler>(new CallAcceptActionHandler(_callHub));
        context.Export<IBusinessEventProvider>(new CallBusinessEventProvider());

        // 5. Attach to the business-event bus if the host provides one.
        var bus = context.Services.GetService(typeof(IBusinessEventBus)) as IBusinessEventBus;
        if (bus is not null)
        {
            _businessEventRelay = new VoipCallBusinessEventRelay(_callHub, bus);
            _businessEventRelay.Attach();
        }
    }

    public async ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        // Tear down in reverse: detach subscriptions, then dispose owned resources.
        _businessEventRelay?.Dispose();
        _businessEventRelay = null;

        if (_callHub is not null)
        {
            await _callHub.ShutdownAsync(cancellationToken).ConfigureAwait(false);
            _callHub = null;
        }
        if (_channelManager is not null)
        {
            await _channelManager.DisposeAsync().ConfigureAwait(false);
            _channelManager = null;
        }
        if (_engine is not null)
        {
            await _engine.DisposeAsync().ConfigureAwait(false);
            _engine = null;
        }
    }

    private static TService ResolveRequired<TService>(IServiceProvider services)
        where TService : class =>
        services.GetService(typeof(TService)) as TService
            ?? throw new InvalidOperationException(
                $"Host service '{typeof(TService).Name}' is required by the plugin.");
}
```

**Expected behavior:** on activation the host constructs `CommunicationPlugin`, calls
`StartAsync`, and the plugin resolves its host services, migrates its schema, and registers
its exports — after which its controllers answer HTTP, its flow actions are callable, and
its business-event provider is live. On deactivation `StopAsync` detaches the event relay,
shuts down the hub, and disposes the channel manager and engine, letting the load context
unload cleanly.

A minimal plugin can be far smaller. The Dialer reference plugin's entire `StartAsync` is
synchronous and just resolves three services and exports two:

```csharp
public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(context);

    var dataStore       = ResolveRequired<IPluginDataStore>(context.Services);
    var channelRegistry = ResolveRequired<ICommunicationChannelRegistry>(context.Services);
    var jobQueue        = ResolveRequired<IBackgroundJobQueue>(context.Services);

    var numberStore = new DataStoreDialNumberStore(dataStore);
    var runStore    = new DataStoreDialRunStore(dataStore);
    var coordinator = new DialRunCoordinator(runStore, jobQueue);

    context.Export<IBackgroundJobHandler>(new DialRunJobHandler(new DialRunExecutor(channelRegistry), numberStore, runStore));
    context.Export<IHostAdminApiExtensionContributor>(new DialerAdminApiExtensionContributor(numberStore, coordinator));

    return ValueTask.CompletedTask;
}

public ValueTask StopAsync(CancellationToken cancellationToken = default) =>
    ValueTask.CompletedTask;
```

Dialer holds no long-lived resources of its own — its dial runs execute as durable host
background jobs — so its `StopAsync` is a no-op. That's the right choice *only* because it
leaks nothing.

## The `[CalloraExtensible]` marker

`IHostManagedPlugin` carries `[CalloraExtensible(...)]`
(`src/Core/Extensibility/CalloraExtensibleAttribute.cs`). This marker declares the type an
**official Callora extension point** — a surface plugins are *meant* to implement, derive
from, or decorate (REV2 §7.1). You'll see it on every sanctioned extension contract
(`IApiController`, `IBusinessEventListener`, `IFlowActionHandler`, …). Absence of the marker
means an API is usable but not a sanctioned extension surface.

The marker is enforced by the **CAL0003** analyzer: marked surfaces must carry XML
documentation, so the extension contract stays legible for implementers. You never apply
`[CalloraExtensible]` yourself — it sits on the *host's* contracts. You just implement the
marked interface.

## Next steps

- Resolve services and export from the context: **[Dependency injection](./dependency-injection)**
- Declare your identity and metadata: **[The registry manifest](./registry-manifest)**
- Every export mechanism in depth: **[Backend Extensions](/guides/backend-extensions)**
- The `IHostManagedPlugin` reference: **[.NET API reference](/api/)**
