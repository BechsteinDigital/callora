using Callora.Core.Application.Persistence.Contracts;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Domain.Plugins.Contracts;

namespace Callora.TestPlugin.Exporting;

/// <summary>
/// Minimal host-managed plugin used only by the RuntimePluginHost activation
/// harness. It exports one contract in <see cref="StartAsync"/> so the test can
/// prove the whole runtime chain: a plugin's <c>context.Export</c> is resolvable
/// by the host across the plugin ALC (unified <c>Callora.*</c> contract type),
/// and the export is withdrawn on deactivation.
/// </summary>
public sealed class ExportingTestPlugin : IHostManagedPlugin
{
    public string PluginId => "exporting-test-plugin";

    public string DisplayName => "Exporting Test Plugin";

    public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Export<IWorkspaceDataPurgeContributor>(new NoopPurgeContributor());
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    private sealed class NoopPurgeContributor : IWorkspaceDataPurgeContributor
    {
        public Task PurgeWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
