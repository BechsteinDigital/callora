using Callora.Core.Domain.Extensions;

namespace Callora.Core.Application.Extensions;

/// <summary>
/// Stores the platform extension-point registry used by admin/workspace APIs and install-time validation.
/// </summary>
public interface IExtensionPointRegistryStore
{
    ValueTask<ExtensionPointRegistrySnapshot> ReplaceAsync(
        string registryVersion,
        IReadOnlyCollection<ExtensionPointDefinition> extensionPoints,
        CancellationToken cancellationToken = default);

    ValueTask<ExtensionPointRegistrySnapshot> GetAllAsync(CancellationToken cancellationToken = default);

    ValueTask<ExtensionPointRegistrySnapshot> GetBySurfaceAsync(
        ExtensionSurface surface,
        CancellationToken cancellationToken = default);

    ValueTask<ExtensionPointDefinition?> FindByIdAsync(
        string extensionPointId,
        CancellationToken cancellationToken = default);
}
