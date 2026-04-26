using VoipHost.PluginContracts.Application.Plugins;
using VoipHost.PluginContracts.Domain.Plugins;
using Callora.Plugins.Voip.Application.Admin;

namespace Callora.Plugins.Voip.Application;

/// <summary>
/// Host-loadable plugin entry for the telephony engine package.
/// </summary>
public sealed class VoipPlugin : IHostManagedPlugin
{
    public const string Id = "voip";

    public string PluginId => Id;

    public string DisplayName => "VoIP Engine";

    public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        context.Export<IHostAdminApiExtensionContributor>(new VoipAdminApiExtensionContributor());
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;
}
