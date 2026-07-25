using System.Reflection;
using Callora.Core.Application.Plugins;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Tests.Hosting;

/// <summary>
/// The plugin DbContext factory must hand EF Core the plugin's migrations assembly
/// as a loaded <see cref="Assembly"/> instance — never by name. EF Core resolves a
/// by-name migrations assembly with <c>Assembly.Load</c> from its own (host) load
/// context, which cannot see a plugin assembly living in the plugin's collectible
/// ALC (the production activation failure once EF Core itself is host-unified).
/// </summary>
public sealed class PluginDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_PassesPluginAssemblyInstance_NotName()
    {
        var provider = new CapturingProvider();
        var factory = new PluginDbContextFactory<SampleDbContext>(provider, "sample-plugin");

        // Construction alone exercises ConfigureOptions; the context is never used,
        // so no database provider needs to be configured on the builder.
        using var context = factory.CreateDbContext();

        Assert.NotNull(provider.CapturedAssembly);
        Assert.Same(typeof(SampleDbContext).Assembly, provider.CapturedAssembly);
    }

    private sealed class CapturingProvider : IPluginDbContextProvider
    {
        public Assembly? CapturedAssembly { get; private set; }

        public void ConfigureOptions(DbContextOptionsBuilder builder, Assembly migrationsAssembly) =>
            CapturedAssembly = migrationsAssembly;

        public long GetMigrationLockKey(string pluginId) => 0;
    }

    private sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options);
}
