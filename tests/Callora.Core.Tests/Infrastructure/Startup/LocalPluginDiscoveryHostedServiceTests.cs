using Callora.Core.Application.Persistence;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Domain.Plugins;
using Callora.Core.Infrastructure.Startup;
using Callora.Core.Tests.Support;
using Callora.Core.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Core.Tests.Infrastructure.Startup;

public sealed class LocalPluginDiscoveryHostedServiceTests
{
    [Fact]
    public async Task StartAsync_PrecompiledAssemblyExists_DoesNotBuildProject()
    {
        var pluginRoot = CreatePluginRoot(
            pluginId: "voip",
            assemblyFileName: "Callora.Plugins.Voip.dll");
        var precompiledPath = Path.Combine(pluginRoot, "Callora.Plugins.Voip.dll");
        await File.WriteAllTextAsync(precompiledPath, "binary");

        var services = new ServiceCollection();
        var installationRepository = new InMemoryPluginInstallationRepository();
        var lifecycleService = new RecordingPluginLifecycleService();
        var builder = new RecordingLocalPluginProjectBuilder();
        services.AddScoped<IPluginInstallationRepository>(_ => installationRepository);
        services.AddScoped<IPluginLifecycleService>(_ => lifecycleService);
        var provider = services.BuildServiceProvider();

        var sut = new LocalPluginDiscoveryHostedService(
            provider,
            new CalloraHostingOptions
            {
                AutoLoadPlugins = true,
                AutoActivateInstalledPlugins = true,
                PluginDirectory = Path.GetDirectoryName(pluginRoot)!
            },
            builder,
            NullLogger<LocalPluginDiscoveryHostedService>.Instance);

        await sut.StartAsync(CancellationToken.None);

        Assert.Empty(builder.BuildCalls);
        Assert.Single(lifecycleService.InstallCalls);
        Assert.Equal(precompiledPath, lifecycleService.InstallCalls[0].AssemblyPath);
        Assert.Single(lifecycleService.ActivateCalls);
        Assert.Equal("voip", lifecycleService.ActivateCalls[0].PluginId);
    }

    [Fact]
    public async Task StartAsync_PluginAlreadyInDatabase_SkipsInstallAndBuild()
    {
        var pluginRoot = CreatePluginRoot(
            pluginId: "voip",
            assemblyFileName: "Callora.Plugins.Voip.dll");
        var precompiledPath = Path.Combine(pluginRoot, "Callora.Plugins.Voip.dll");
        await File.WriteAllTextAsync(precompiledPath, "binary");

        var installationRepository = new InMemoryPluginInstallationRepository();
        await installationRepository.AddAsync(
            PluginInstallation.CreateInstalled(
                pluginId: "voip",
                displayName: "Voip",
                assemblyPath: precompiledPath,
                entryTypeName: "Plugin.Entry",
                nowUtc: DateTimeOffset.UtcNow));

        var lifecycleService = new RecordingPluginLifecycleService();
        var builder = new RecordingLocalPluginProjectBuilder();

        var services = new ServiceCollection();
        services.AddScoped<IPluginInstallationRepository>(_ => installationRepository);
        services.AddScoped<IPluginLifecycleService>(_ => lifecycleService);
        var provider = services.BuildServiceProvider();

        var sut = new LocalPluginDiscoveryHostedService(
            provider,
            new CalloraHostingOptions
            {
                AutoLoadPlugins = true,
                AutoActivateInstalledPlugins = true,
                PluginDirectory = Path.GetDirectoryName(pluginRoot)!
            },
            builder,
            NullLogger<LocalPluginDiscoveryHostedService>.Instance);

        await sut.StartAsync(CancellationToken.None);

        Assert.Empty(builder.BuildCalls);
        Assert.Empty(lifecycleService.InstallCalls);
        Assert.Empty(lifecycleService.ActivateCalls);
    }

    private static string CreatePluginRoot(string pluginId, string assemblyFileName)
    {
        var pluginsRoot = Path.Combine(Path.GetTempPath(), "callora-tests", Guid.NewGuid().ToString("N"), "plugins");
        var pluginRoot = Path.Combine(pluginsRoot, "Voip");
        Directory.CreateDirectory(pluginRoot);

        var registryPath = Path.Combine(pluginRoot, "registry.json");
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
        File.WriteAllText(registryPath, json);

        return pluginRoot;
    }
}
