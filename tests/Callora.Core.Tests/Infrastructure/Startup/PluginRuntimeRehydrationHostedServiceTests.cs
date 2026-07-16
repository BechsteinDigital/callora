using Callora.Core.Application.Persistence;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Plugins;
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

    [Fact]
    public async Task StartAsync_ActivatesActiveInstallationsInDependencyOrder()
    {
        var installations = new InMemoryPluginInstallationRepository();
        // dialer requires communication.voice, added first to prove reordering.
        var dialer = PluginInstallation.CreateInstalled("dialer", "Dialer", "/tmp/dialer.dll", "Dialer.Entry", DateTimeOffset.UtcNow);
        dialer.MarkActivated(DateTimeOffset.UtcNow);
        var communication = PluginInstallation.CreateInstalled("communication", "Communication", "/tmp/communication.dll", "Comm.Entry", DateTimeOffset.UtcNow);
        communication.MarkActivated(DateTimeOffset.UtcNow);
        await installations.AddAsync(dialer);
        await installations.AddAsync(communication);

        var lifecycle = new RecordingPluginLifecycleService();
        var reader = new FakeRegistryReader(new Dictionary<string, PluginPackageRegistryMetadata>
        {
            ["/tmp/communication.dll"] = Meta("communication", provides: ["communication.voice"], requires: []),
            ["/tmp/dialer.dll"] = Meta("dialer", provides: [], requires: ["communication.voice"]),
        });

        var services = new ServiceCollection();
        services.AddScoped<IPluginInstallationRepository>(_ => installations);
        services.AddScoped<IPluginLifecycleService>(_ => lifecycle);
        services.AddScoped<IPluginPackageRegistryReader>(_ => reader);
        await using var provider = services.BuildServiceProvider();

        var sut = new PluginRuntimeRehydrationHostedService(
            provider,
            NullLogger<PluginRuntimeRehydrationHostedService>.Instance);

        await sut.StartAsync(CancellationToken.None);

        Assert.Equal(["communication", "dialer"], lifecycle.ActivateCalls.Select(call => call.PluginId).ToArray());
    }

    private static PluginPackageRegistryMetadata Meta(string id, string[] provides, string[] requires)
        => new("v1", "1.0", id, id, "1.0.0", id + ".dll", id + ".Entry", provides, new Dictionary<string, string>(), null, requires);

    private sealed class FakeRegistryReader : IPluginPackageRegistryReader
    {
        private readonly IReadOnlyDictionary<string, PluginPackageRegistryMetadata> _byPath;

        public FakeRegistryReader(IReadOnlyDictionary<string, PluginPackageRegistryMetadata> byPath) => _byPath = byPath;

        public ValueTask<PluginPackageRegistryReadResult> ReadForAssemblyAsync(string assemblyPath, CancellationToken cancellationToken = default)
        {
            _byPath.TryGetValue(assemblyPath, out var metadata);
            return new ValueTask<PluginPackageRegistryReadResult>(
                new PluginPackageRegistryReadResult(metadata is not null, metadata is not null, assemblyPath, metadata));
        }
    }
}
