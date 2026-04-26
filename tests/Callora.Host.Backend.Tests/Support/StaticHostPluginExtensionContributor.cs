using VoipHost.PluginContracts.Application.Plugins;

namespace Callora.Host.Backend.Tests.Support;

internal sealed class StaticHostPluginExtensionContributor(
    string pluginId,
    IReadOnlyList<string> capabilities,
    IReadOnlyList<HostPluginExtensionRegistration> registrations) : IHostPluginExtensionContributor
{
    public string PluginId { get; } = pluginId;

    public IReadOnlyList<string> Capabilities { get; } = capabilities;

    public IReadOnlyList<HostPluginExtensionRegistration> GetRegistrations() => registrations;
}
