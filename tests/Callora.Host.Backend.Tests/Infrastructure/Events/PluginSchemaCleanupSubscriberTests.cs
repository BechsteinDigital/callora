using Callora.Host.Backend.Application.Persistence;
using Callora.Host.Backend.Application.Events;
using Callora.Host.Backend.Domain.Plugins;
using Callora.Host.Backend.Infrastructure.Events;
using Callora.Host.Backend.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Host.Backend.Tests.Infrastructure.Events;

public sealed class PluginSchemaCleanupSubscriberTests
{
    [Fact]
    public async Task Uninstall_WithoutManifest_DropsConventionSchema()
    {
        var dropper = new RecordingSchemaDropper();
        var subscriber = CreateSubscriber(dropper, installations: new InMemoryPluginInstallationRepository());

        await subscriber.HandleAsync(UninstallEvent("acme-dialer", success: true));

        Assert.Equal(["plugin_acme_dialer"], dropper.Dropped);
    }

    [Fact]
    public async Task Uninstall_WithManifestSchema_PrefersDeclaredName()
    {
        using var workspace = new TempWorkspace();
        var pluginDir = workspace.CreateDirectory("plugin");
        var assemblyPath = Path.Combine(pluginDir, "plugin.dll");
        File.WriteAllText(assemblyPath, "stub");
        File.WriteAllText(Path.Combine(pluginDir, "registry.json"),
            """{ "pluginId": "voip", "databaseSchema": "custom_voip_schema" }""");

        var installations = new InMemoryPluginInstallationRepository();
        await installations.AddAsync(PluginInstallation.CreateInstalled("voip", "Voice", assemblyPath, null, DateTimeOffset.UtcNow));

        var dropper = new RecordingSchemaDropper();
        var subscriber = CreateSubscriber(dropper, installations);

        await subscriber.HandleAsync(UninstallEvent("voip", success: true));

        Assert.Equal(["custom_voip_schema"], dropper.Dropped);
    }

    [Fact]
    public async Task NonUninstallActions_DoNotDrop()
    {
        var dropper = new RecordingSchemaDropper();
        var subscriber = CreateSubscriber(dropper, new InMemoryPluginInstallationRepository());

        await subscriber.HandleAsync(new PluginLifecycleChangedEvent(DateTimeOffset.UtcNow, "plugin.install", "voip", true, "tester", null));
        await subscriber.HandleAsync(new PluginLifecycleChangedEvent(DateTimeOffset.UtcNow, "plugin.activate", "voip", true, "tester", null));

        Assert.Empty(dropper.Dropped);
    }

    [Fact]
    public async Task FailedUninstall_DoesNotDrop()
    {
        var dropper = new RecordingSchemaDropper();
        var subscriber = CreateSubscriber(dropper, new InMemoryPluginInstallationRepository());

        await subscriber.HandleAsync(UninstallEvent("voip", success: false));

        Assert.Empty(dropper.Dropped);
    }

    [Fact]
    public async Task DropperFailure_IsIsolated()
    {
        var subscriber = CreateSubscriber(new ThrowingSchemaDropper(), new InMemoryPluginInstallationRepository());

        // Must not throw — a failed drop is logged, not propagated.
        await subscriber.HandleAsync(UninstallEvent("voip", success: true));
    }

    private static PluginSchemaCleanupSubscriber CreateSubscriber(
        IPluginSchemaDropper dropper,
        IPluginInstallationRepository installations) =>
        new(dropper, installations, NullLogger<PluginSchemaCleanupSubscriber>.Instance);

    private static PluginLifecycleChangedEvent UninstallEvent(string pluginId, bool success) =>
        new(DateTimeOffset.UtcNow, "plugin.uninstall", pluginId, success, "tester", null);

    private sealed class RecordingSchemaDropper : IPluginSchemaDropper
    {
        public List<string> Dropped { get; } = [];

        public Task DropAsync(string schemaName, CancellationToken cancellationToken = default)
        {
            Dropped.Add(schemaName);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingSchemaDropper : IPluginSchemaDropper
    {
        public Task DropAsync(string schemaName, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("drop failed");
    }
}
