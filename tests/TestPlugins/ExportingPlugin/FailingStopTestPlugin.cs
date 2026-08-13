using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Domain.Plugins.Contracts;

namespace Callora.TestPlugin.Exporting;

/// <summary>
/// A plugin whose teardown fails. It exists so the host harness can reach the error branch of a
/// deactivation for real instead of simulating it — what the host records about a plugin it could
/// not stop is only observable when a stop actually throws.
/// </summary>
public sealed class FailingStopTestPlugin : IHostManagedPlugin
{
    public string PluginId => "failing-stop-test-plugin";

    public string DisplayName => "Failing Stop Test Plugin";

    public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("This plugin refuses to stop.");
}
