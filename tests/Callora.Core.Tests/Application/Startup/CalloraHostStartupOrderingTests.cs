using Callora.Core.Application.Options;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Startup;
using Microsoft.Extensions.DependencyInjection;

namespace Callora.Core.Tests.Application.Startup;

public sealed class CalloraHostStartupOrderingTests
{
    [Fact]
    public async Task StartAsync_ActivatesPluginsInDependencyOrder()
    {
        // Dialer requires communication.voice, which Communication provides. Loaded
        // dialer-first to prove activation is reordered by dependency, not discovery.
        var dialer = Descriptor("dialer");
        var communication = Descriptor("communication");
        var runtime = new RecordingRuntime(dialer, communication);

        var reader = new FakeRegistryReader(new Dictionary<string, PluginPackageRegistryMetadata>
        {
            ["communication"] = Meta("communication", provides: ["communication.voice"], requires: []),
            ["dialer"] = Meta("dialer", provides: [], requires: ["communication.voice"]),
        });

        var services = new ServiceCollection()
            .AddSingleton<IPluginPackageRegistryReader>(reader)
            .BuildServiceProvider();

        var options = new CalloraHostingOptions { AutoLoadPlugins = false, AutoActivateInstalledPlugins = true };
        var startup = new CalloraHostStartup(options, runtime);

        await startup.StartAsync(services);

        Assert.Equal(["communication", "dialer"], runtime.Activated);
    }

    [Fact]
    public async Task StartAsync_WithoutRegistryReader_KeepsDiscoveryOrder()
    {
        var runtime = new RecordingRuntime(Descriptor("b"), Descriptor("a"));
        var services = new ServiceCollection().BuildServiceProvider();
        var options = new CalloraHostingOptions { AutoLoadPlugins = false, AutoActivateInstalledPlugins = true };

        await new CalloraHostStartup(options, runtime).StartAsync(services);

        Assert.Equal(["b", "a"], runtime.Activated);
    }

    private static RuntimePluginDescriptor Descriptor(string id)
        => new(id, id, id, null, RuntimePluginState.Inactive);

    private static PluginPackageRegistryMetadata Meta(string id, string[] provides, string[] requires)
        => new("v1", "1.0", id, id, "1.0.0", id + ".dll", id + ".Entry", provides, new Dictionary<string, string>(), null, requires);

    private sealed class RecordingRuntime : ICalloraPluginRuntime
    {
        private readonly List<RuntimePluginDescriptor> _loaded;

        public RecordingRuntime(params RuntimePluginDescriptor[] loaded) => _loaded = [.. loaded];

        public List<string> Activated { get; } = [];

        public IReadOnlyCollection<RuntimePluginDescriptor> LoadedPlugins => _loaded;

        public Task<RuntimePluginActivateResult> ActivateAsync(string pluginId, CancellationToken cancellationToken = default)
        {
            Activated.Add(pluginId);
            return Task.FromResult(new RuntimePluginActivateResult(RuntimePluginActivateStatus.Activated, pluginId));
        }

        public Task<RuntimePluginInstallResult> InstallAsync(string assemblyPath, string? entryTypeName = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RuntimePluginDeactivateResult> DeactivateAsync(string pluginId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RuntimePluginUninstallResult> UninstallAsync(string pluginId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public bool TryGetExport(Type contractType, out object? service)
        {
            service = null;
            return false;
        }

        public IReadOnlyList<object> GetExports(Type contractType) => [];

        public IReadOnlyList<CalloraPluginExport> GetOwnedExports(Type contractType) => [];
    }

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
