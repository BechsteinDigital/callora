using Callora.Modules.Abstractions.Application.Plugins;
using VoipHost.PluginContracts.Application.Plugins;
using VoipHost.PluginContracts.Domain.Plugins;

namespace Callora.Hosting.Application.Plugins;

internal sealed class LegacyRuntimePluginAdapter(ICalloraRuntimePlugin inner) : IHostManagedPlugin
{
    public string PluginId => inner.PluginId;

    public string DisplayName => inner.DisplayName;

    public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        if (context is not ICalloraPluginContext voipContext)
        {
            throw new InvalidOperationException(
                $"Expected context implementing {nameof(ICalloraPluginContext)}.");
        }

        return inner.StartAsync(voipContext, cancellationToken);
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default) =>
        inner.StopAsync(cancellationToken);
}
