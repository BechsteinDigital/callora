using Callora.Host.Backend.Application.Jobs;
using Callora.Host.Backend.Tests.Support;
using Callora.Host.PluginContracts.Application.Data;
using Callora.Host.PluginContracts.Application.Jobs;
using Callora.Hosting.Application.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Callora.Host.Backend.Tests.Hosting;

public sealed class CuratedPluginServiceProviderTests
{
    [Fact]
    public void ResolvesContractsAndLogging_ButBlocksHostInternals()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IBackgroundJobQueue, RecordingBackgroundJobQueue>();
        // Host-interner Dienst, den Plugins nicht sehen dürfen:
        services.AddSingleton<IBackgroundJobStore, Callora.Host.Backend.Application.Jobs.InMemoryBackgroundJobStore>();
        using var root = services.BuildServiceProvider();

        var curated = new CuratedPluginServiceProvider(root, "acme-plugin");

        Assert.NotNull(curated.GetService(typeof(IBackgroundJobQueue)));
        Assert.NotNull(curated.GetService(typeof(ILoggerFactory)));
        Assert.NotNull(curated.GetService(typeof(ILogger<CuratedPluginServiceProviderTests>)));
        Assert.Null(curated.GetService(typeof(IBackgroundJobStore)));
        Assert.Null(curated.GetService(typeof(IServiceScopeFactory)));
    }

    [Fact]
    public async Task DataStore_IsPluginBound_AndRejectsForeignPluginIds()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPluginDataStore>(new Callora.Host.Backend.Application.Plugins.InMemoryPluginDataStore());
        using var root = services.BuildServiceProvider();

        var curated = new CuratedPluginServiceProvider(root, "acme-plugin");
        var dataStore = Assert.IsAssignableFrom<IPluginDataStore>(curated.GetService(typeof(IPluginDataStore)));

        await dataStore.SetAsync(new PluginDataKey("acme-plugin", null, "settings", "a"), "{}");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dataStore.GetAsync(new PluginDataKey("other-plugin", null, "settings", "a")));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dataStore.ListAsync(new PluginDataCollectionKey("other-plugin", null, "settings")));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dataStore.ListWorkspaceKeysAsync("other-plugin", "settings"));
    }
}
