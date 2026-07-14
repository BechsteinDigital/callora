using Callora.Host.Backend.Application.Policies;
using Callora.Host.Backend.Infrastructure.Persistence;
using Callora.Host.PluginContracts.Application.Persistence;
using Callora.Hosting.Application.Plugins;
using Callora.Plugins.Voip.Application.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Host.Backend.Tests.Hosting;

public sealed class PluginDbContextFactoryTests
{
    private static NpgsqlPluginDbContextProvider CreateProvider() =>
        new(new BackendHostOptions
        {
            DatabaseConnectionString = "Host=localhost;Database=callora_test;Username=callora;Password=callora"
        });

    [Fact]
    public void CreateDbContext_BuildsTypedContext_InPluginSchema()
    {
        // CreateDbContext only builds options; it does not open a connection.
        var factory = new PluginDbContextFactory<VoipDbContext>(CreateProvider(), "voip");

        using var db = factory.CreateDbContext();
        var entity = db.Model.FindEntityType(typeof(CallLog));

        Assert.NotNull(entity);
        Assert.Equal(VoipDbContext.SchemaName, entity!.GetSchema());
        Assert.Equal("call_logs", entity.GetTableName());
    }

    [Fact]
    public void GetMigrationLockKey_IsDeterministic_AndDistinctPerPlugin()
    {
        var provider = CreateProvider();

        Assert.Equal(provider.GetMigrationLockKey("voip"), provider.GetMigrationLockKey("voip"));
        Assert.NotEqual(provider.GetMigrationLockKey("voip"), provider.GetMigrationLockKey("dialer"));
    }

    [Fact]
    public void CuratedProvider_ResolvesPluginDbContextFactory_ButNotForeignServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPluginDbContextProvider>(CreateProvider());
        using var root = services.BuildServiceProvider();

        var curated = new CuratedPluginServiceProvider(root, "voip");

        var factory = curated.GetService(typeof(IPluginDbContextFactory<VoipDbContext>));
        Assert.NotNull(factory);
        Assert.IsAssignableFrom<IPluginDbContextFactory<VoipDbContext>>(factory);

        // Host internals stay unreachable through the curated surface.
        Assert.Null(curated.GetService(typeof(IPluginDbContextProvider)));
    }
}
