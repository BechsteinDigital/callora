using Callora.Core.Application.Extensions;
using Callora.Core.Application.Plugins;

namespace Callora.Core.Application.Extensions;

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
