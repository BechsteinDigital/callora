using Callora.Host.Backend.Application.Abstractions.Extensions;
using Callora.Host.Backend.Application.Abstractions.Plugins;

namespace Callora.Host.Backend.Application.Lifecycle;

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
