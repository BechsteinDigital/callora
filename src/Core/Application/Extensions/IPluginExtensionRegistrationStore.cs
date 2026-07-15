using Callora.Core.Application.Plugins;

namespace Callora.Core.Application.Extensions;

/// <summary>
/// Stores plugin-declared extension registrations and capabilities for runtime discovery.
/// </summary>
public interface IPluginExtensionRegistrationStore
{
    ValueTask UpsertAsync(
        string pluginId,
        IReadOnlyList<PluginPackageExtensionRegistration> extensionRegistrations,
        IReadOnlyList<string> capabilities,
        CancellationToken cancellationToken = default);

    ValueTask RemoveAsync(
        string pluginId,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<PluginExtensionRegistrationSnapshot>> ListAsync(
        CancellationToken cancellationToken = default);
}
