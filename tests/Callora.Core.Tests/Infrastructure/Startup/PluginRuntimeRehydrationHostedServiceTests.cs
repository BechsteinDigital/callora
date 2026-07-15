using Callora.Core.Application.Persistence;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Domain.Plugins;
using Callora.Core.Infrastructure.Startup;
using Callora.Core.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Core.Tests.Infrastructure.Startup;

public sealed class PluginRuntimeRehydrationHostedServiceTests
{
    [Fact]
    public async Task StartAsync_ActiveInstallation_RehydratesViaLifecycleInstallAndActivate()
    {
        var installations = new InMemoryPluginInstallationRepository();
        var active = PluginInstallation.CreateInstalled(
            pluginId: "voip",
            displayName: "Voip",
            assemblyPath: "/tmp/voip.dll",
            entryTypeName: "Voip.Entry",
            nowUtc: DateTimeOffset.UtcNow);
        active.MarkActivated(DateTimeOffset.UtcNow);
        await installations.AddAsync(active);

        var lifecycle = new RecordingPluginLifecycleService();

        var services = new ServiceCollection();
        services.AddScoped<IPluginInstallationRepository>(_ => installations);
        services.AddScoped<IPluginLifecycleService>(_ => lifecycle);
        await using var provider = services.BuildServiceProvider();

        var sut = new PluginRuntimeRehydrationHostedService(
            provider,
            NullLogger<PluginRuntimeRehydrationHostedService>.Instance);

        await sut.StartAsync(CancellationToken.None);

        Assert.Single(lifecycle.InstallCalls);
        Assert.Equal("/tmp/voip.dll", lifecycle.InstallCalls[0].AssemblyPath);
        Assert.Equal("Voip.Entry", lifecycle.InstallCalls[0].EntryTypeName);
        Assert.Equal("system:runtime-rehydration", lifecycle.InstallCalls[0].RequestedBy);

        Assert.Single(lifecycle.ActivateCalls);
        Assert.Equal("voip", lifecycle.ActivateCalls[0].PluginId);
        Assert.Equal("system:runtime-rehydration", lifecycle.ActivateCalls[0].RequestedBy);
        Assert.Null(lifecycle.ActivateCalls[0].WorkspaceKey);
    }

    [Fact]
    public async Task StartAsync_InactiveInstallation_RehydratesViaLifecycleInstallWithoutActivate()
    {
        var installations = new InMemoryPluginInstallationRepository();
        var inactive = PluginInstallation.CreateInstalled(
            pluginId: "voip",
            displayName: "Voip",
            assemblyPath: "/tmp/voip.dll",
            entryTypeName: "Voip.Entry",
            nowUtc: DateTimeOffset.UtcNow);
        inactive.MarkDeactivated(DateTimeOffset.UtcNow);
        await installations.AddAsync(inactive);

        var lifecycle = new RecordingPluginLifecycleService();

        var services = new ServiceCollection();
        services.AddScoped<IPluginInstallationRepository>(_ => installations);
        services.AddScoped<IPluginLifecycleService>(_ => lifecycle);
        await using var provider = services.BuildServiceProvider();

        var sut = new PluginRuntimeRehydrationHostedService(
            provider,
            NullLogger<PluginRuntimeRehydrationHostedService>.Instance);

        await sut.StartAsync(CancellationToken.None);

        Assert.Single(lifecycle.InstallCalls);
        Assert.Empty(lifecycle.ActivateCalls);
    }
}
