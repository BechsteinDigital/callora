using Callora.Core.Application.Extensions;
using Callora.Core.Domain.Extensions;

namespace Callora.Core.Application.Extensions;

internal sealed class EmptyExtensionPointRegistryStore : IExtensionPointRegistryStore
{
    private static readonly ExtensionPointRegistrySnapshot EmptySnapshot =
        new("1.0", Array.Empty<ExtensionPointDefinition>());

    public ValueTask<ExtensionPointRegistrySnapshot> ReplaceAsync(
        string registryVersion,
        IReadOnlyCollection<ExtensionPointDefinition> extensionPoints,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(EmptySnapshot);

    public ValueTask<ExtensionPointRegistrySnapshot> GetAllAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(EmptySnapshot);

    public ValueTask<ExtensionPointRegistrySnapshot> GetBySurfaceAsync(
        ExtensionSurface surface,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(EmptySnapshot);

    public ValueTask<ExtensionPointDefinition?> FindByIdAsync(
        string extensionPointId,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<ExtensionPointDefinition?>(null);
}
