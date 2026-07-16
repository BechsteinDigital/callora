using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Options;
using Callora.Core.Application.Persistence;
using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Plugins;
using Callora.Core.Infrastructure.Startup;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Core.Tests.Infrastructure.Startup;

public sealed class LocalPluginDiscoveryHostedServiceTests
{
    [Fact]
    public async Task StartAsync_PrecompiledAssemblyExists_InstallsAndActivatesWithoutBuild()
    {
        var (pluginsRoot, pluginRoot) = CreatePluginRoot("voip", "Callora.Plugins.Voip.dll");
        var precompiledPath = Path.GetFullPath(Path.Combine(pluginRoot, "Callora.Plugins.Voip.dll"));
        await File.WriteAllTextAsync(precompiledPath, "binary");

        var repo = new InMemoryPluginInstallationRepository();
        var lifecycle = new RecordingPluginLifecycleService();
        var builder = new RecordingLocalPluginProjectBuilder();
        var options = Options(pluginsRoot, autoActivate: true);
        var provider = BuildProvider(options, repo, lifecycle, builder);

        var sut = new LocalPluginDiscoveryHostedService(provider, options, NullLogger<LocalPluginDiscoveryHostedService>.Instance);
        await sut.StartAsync(CancellationToken.None);

        Assert.Empty(builder.BuildCalls);
        Assert.Single(lifecycle.InstallCalls);
        Assert.Equal(precompiledPath, lifecycle.InstallCalls[0].AssemblyPath);
        Assert.Single(lifecycle.ActivateCalls);
        Assert.Equal("voip", lifecycle.ActivateCalls[0].PluginId);
    }

    [Fact]
    public async Task StartAsync_PluginAlreadyInDatabaseUnchanged_SkipsInstallAndBuild()
    {
        var (pluginsRoot, pluginRoot) = CreatePluginRoot("voip", "Callora.Plugins.Voip.dll");
        var precompiledPath = Path.GetFullPath(Path.Combine(pluginRoot, "Callora.Plugins.Voip.dll"));
        await File.WriteAllTextAsync(precompiledPath, "binary");

        var repo = new InMemoryPluginInstallationRepository();
        await repo.AddAsync(PluginInstallation.CreateInstalled("voip", "Voip", precompiledPath, "Plugin.Entry", DateTimeOffset.UtcNow));
        var lifecycle = new RecordingPluginLifecycleService();
        var builder = new RecordingLocalPluginProjectBuilder();
        var options = Options(pluginsRoot, autoActivate: true);
        var provider = BuildProvider(options, repo, lifecycle, builder);

        var sut = new LocalPluginDiscoveryHostedService(provider, options, NullLogger<LocalPluginDiscoveryHostedService>.Instance);
        await sut.StartAsync(CancellationToken.None);

        Assert.Empty(builder.BuildCalls);
        Assert.Empty(lifecycle.InstallCalls);
        Assert.Empty(lifecycle.UpdateCalls);
        Assert.Empty(lifecycle.ActivateCalls);
    }

    [Fact]
    public async Task RefreshAsync_ChangedManifest_UpdatesFromLocal()
    {
        var (pluginsRoot, pluginRoot) = CreatePluginRoot("voip", "Callora.Plugins.Voip.dll");
        var precompiledPath = Path.GetFullPath(Path.Combine(pluginRoot, "Callora.Plugins.Voip.dll"));
        await File.WriteAllTextAsync(precompiledPath, "binary");

        var repo = new InMemoryPluginInstallationRepository();
        // Stored entry type differs from the manifest ("Plugin.Entry") -> changed.
        await repo.AddAsync(PluginInstallation.CreateInstalled("voip", "Voip", precompiledPath, "Old.Entry", DateTimeOffset.UtcNow));
        var lifecycle = new RecordingPluginLifecycleService();
        var sut = new LocalPluginDiscoveryService(
            Options(pluginsRoot, autoActivate: false),
            repo,
            lifecycle,
            new RecordingLocalPluginProjectBuilder(),
            NullLogger<LocalPluginDiscoveryService>.Instance);

        var result = await sut.RefreshAsync(CancellationToken.None);

        Assert.Equal(["voip"], result.Updated);
        Assert.Single(lifecycle.UpdateCalls);
        Assert.Empty(lifecycle.InstallCalls);
    }

    [Fact]
    public async Task RefreshAsync_InactivePluginWithMissingAssemblyUnderScanRoot_Uninstalls()
    {
        var pluginsRoot = CreateEmptyPluginsRoot();
        var gonePath = Path.Combine(pluginsRoot, "Ghost", "Ghost.dll");
        var installation = PluginInstallation.CreateInstalled("ghost", "Ghost", gonePath, "Ghost.Entry", DateTimeOffset.UtcNow);
        installation.MarkDeactivated(DateTimeOffset.UtcNow);

        var (result, lifecycle) = await RunRefreshAsync(pluginsRoot, installation);

        Assert.Equal(["ghost"], result.RemovedInactive);
        Assert.Single(lifecycle.UninstallCalls);
        Assert.Equal("ghost", lifecycle.UninstallCalls[0].PluginId);
    }

    [Fact]
    public async Task RefreshAsync_ActivePluginWithMissingAssembly_KeptAndReported()
    {
        var pluginsRoot = CreateEmptyPluginsRoot();
        var gonePath = Path.Combine(pluginsRoot, "Ghost", "Ghost.dll");
        var installation = PluginInstallation.CreateInstalled("ghost", "Ghost", gonePath, "Ghost.Entry", DateTimeOffset.UtcNow);
        installation.MarkActivated(DateTimeOffset.UtcNow);

        var (result, lifecycle) = await RunRefreshAsync(pluginsRoot, installation);

        Assert.Equal(["ghost"], result.MissingActive);
        Assert.Empty(result.RemovedInactive);
        Assert.Empty(lifecycle.UninstallCalls);
    }

    [Fact]
    public async Task RefreshAsync_MissingPluginOutsideScanRoots_LeftUntouched()
    {
        var pluginsRoot = CreateEmptyPluginsRoot();
        // A NuGet-/operator-installed plugin lives outside the scan roots — refresh must not touch it.
        var installation = PluginInstallation.CreateInstalled("nuget-plugin", "NuGet", "/opt/other/NuGet.dll", "NuGet.Entry", DateTimeOffset.UtcNow);
        installation.MarkDeactivated(DateTimeOffset.UtcNow);

        var (result, lifecycle) = await RunRefreshAsync(pluginsRoot, installation);

        Assert.Empty(result.RemovedInactive);
        Assert.Empty(lifecycle.UninstallCalls);
    }

    private static async Task<(PluginDiscoveryRefreshResult Result, RecordingPluginLifecycleService Lifecycle)> RunRefreshAsync(
        string pluginsRoot,
        PluginInstallation installation)
    {
        var repo = new InMemoryPluginInstallationRepository();
        await repo.AddAsync(installation);
        var lifecycle = new RecordingPluginLifecycleService();
        var sut = new LocalPluginDiscoveryService(
            Options(pluginsRoot, autoActivate: false),
            repo,
            lifecycle,
            new RecordingLocalPluginProjectBuilder(),
            NullLogger<LocalPluginDiscoveryService>.Instance);

        var result = await sut.RefreshAsync(CancellationToken.None);
        return (result, lifecycle);
    }

    private static CalloraHostingOptions Options(string pluginsRoot, bool autoActivate) => new()
    {
        AutoLoadPlugins = true,
        AutoActivateInstalledPlugins = autoActivate,
        PluginDirectory = pluginsRoot,
        StaticPluginDirectory = string.Empty,
    };

    private static ServiceProvider BuildProvider(
        CalloraHostingOptions options,
        InMemoryPluginInstallationRepository repo,
        RecordingPluginLifecycleService lifecycle,
        RecordingLocalPluginProjectBuilder builder)
    {
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddScoped<IPluginInstallationRepository>(_ => repo);
        services.AddScoped<IPluginLifecycleService>(_ => lifecycle);
        services.AddScoped<ILocalPluginProjectBuilder>(_ => builder);
        services.AddScoped<ILogger<LocalPluginDiscoveryService>>(_ => NullLogger<LocalPluginDiscoveryService>.Instance);
        services.AddScoped<IPluginDiscoveryService, LocalPluginDiscoveryService>();
        return services.BuildServiceProvider();
    }

    private static string CreateEmptyPluginsRoot()
    {
        var pluginsRoot = Path.Combine(Path.GetTempPath(), "callora-tests", Guid.NewGuid().ToString("N"), "plugins");
        Directory.CreateDirectory(pluginsRoot);
        return pluginsRoot;
    }

    private static (string PluginsRoot, string PluginRoot) CreatePluginRoot(string pluginId, string assemblyFileName)
    {
        var pluginsRoot = CreateEmptyPluginsRoot();
        var pluginRoot = Path.Combine(pluginsRoot, "Voip");
        Directory.CreateDirectory(pluginRoot);

        var json = $$"""
                     {
                       "contractVersion": "v1",
                       "schemaVersion": "1.0",
                       "name": "Voip Plugin",
                       "pluginId": "{{pluginId}}",
                       "version": "1.0.0",
                       "assemblyFileName": "{{assemblyFileName}}",
                       "entryTypeName": "Plugin.Entry"
                     }
                     """;
        File.WriteAllText(Path.Combine(pluginRoot, "registry.json"), json);
        return (pluginsRoot, pluginRoot);
    }
}
