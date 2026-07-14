using Callora.Host.Backend.Application.Extensions;
using Callora.Host.Backend.Application.Plugins;

namespace Callora.Host.Backend.Application.Extensions;

internal sealed class EmptyPluginExtensionRegistrationStore : IPluginExtensionRegistrationStore
{
    public ValueTask UpsertAsync(
        string pluginId,
        IReadOnlyList<PluginPackageExtensionRegistration> extensionRegistrations,
        IReadOnlyList<string> capabilities,
        CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public ValueTask RemoveAsync(string pluginId, CancellationToken cancellationToken = default) =>
        ValueTask.CompletedTask;

    public ValueTask<IReadOnlyList<PluginExtensionRegistrationSnapshot>> ListAsync(
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<IReadOnlyList<PluginExtensionRegistrationSnapshot>>([]);
}
