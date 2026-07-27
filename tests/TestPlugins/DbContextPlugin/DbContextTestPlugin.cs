using Callora.Core.Application.Persistence.Contracts;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Domain.Plugins.Contracts;

namespace Callora.TestPlugin.DbContextPlugin;

/// <summary>
/// Test-fixture plugin whose <see cref="StartAsync"/> forces the runtime to load
/// the closed generic <c>IPluginDbContextFactory&lt;PluginTestDbContext&gt;</c> and
/// validate its <c>where TContext : DbContext</c> constraint. Because
/// <see cref="PluginTestDbContext"/> derives from the host-provided EF Core
/// <c>DbContext</c>, activation succeeds only when the plugin ALC unifies EF Core
/// to the host's copy — the regression this fixture guards (a bundled plugin that
/// ships its own EF Core copy must not get a duplicate <c>DbContext</c> identity).
/// </summary>
public sealed class DbContextTestPlugin : IHostManagedPlugin
{
    /// <summary>
    /// The closed-generic factory type the plugin resolved. Assigning it is an
    /// observable side effect the JIT cannot elide, so the constraint-triggering
    /// type load always runs (a discarded <c>typeof</c> would be optimized away
    /// in Release and never exercise the regression).
    /// </summary>
    public static Type? ResolvedFactoryType { get; private set; }

    public string PluginId => "dbcontext-test-plugin";

    public string DisplayName => "DbContext Test Plugin";

    public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Mirror how a real plugin resolves its DbContext factory: referencing the
        // closed generic loads it and validates the (where TContext : DbContext)
        // constraint. With a duplicate EF Core, PluginTestDbContext derives from a
        // different DbContext identity and this throws TypeLoadException — the exact
        // production failure. With host-unified EF Core it resolves cleanly.
        ResolvedFactoryType = typeof(IPluginDbContextFactory<PluginTestDbContext>);
        _ = context.Services.GetService(ResolvedFactoryType);

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
