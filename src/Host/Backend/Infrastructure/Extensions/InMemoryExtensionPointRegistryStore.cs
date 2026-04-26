using Callora.Host.Backend.Application.Abstractions.Extensions;
using Callora.Host.Backend.Domain.Extensions;

namespace Callora.Host.Backend.Infrastructure.Extensions;

public sealed class InMemoryExtensionPointRegistryStore : IExtensionPointRegistryStore
{
    private readonly object _sync = new();
    private string _registryVersion;
    private Dictionary<string, ExtensionPointDefinition> _extensionPoints;

    public InMemoryExtensionPointRegistryStore()
    {
        _registryVersion = BackendExtensionPointCatalog.Version;
        _extensionPoints = BackendExtensionPointCatalog.Build()
            .ToDictionary(x => x.ExtensionPointId, x => x, StringComparer.OrdinalIgnoreCase);
    }

    public ValueTask<ExtensionPointRegistrySnapshot> ReplaceAsync(
        string registryVersion,
        IReadOnlyCollection<ExtensionPointDefinition> extensionPoints,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(registryVersion);

        var next = new Dictionary<string, ExtensionPointDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var extensionPoint in extensionPoints)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(extensionPoint.ExtensionPointId))
            {
                throw new ArgumentException("ExtensionPointId is required.", nameof(extensionPoints));
            }

            var normalizedId = extensionPoint.ExtensionPointId.Trim();
            if (!next.TryAdd(normalizedId, extensionPoint with { ExtensionPointId = normalizedId }))
            {
                throw new ArgumentException(
                    $"Duplicate extensionPointId '{normalizedId}'.",
                    nameof(extensionPoints));
            }
        }

        lock (_sync)
        {
            _registryVersion = registryVersion.Trim();
            _extensionPoints = next;
            return ValueTask.FromResult(CreateSnapshotUnsafe(static _ => true));
        }
    }

    public ValueTask<ExtensionPointRegistrySnapshot> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return ValueTask.FromResult(CreateSnapshotUnsafe(static _ => true));
        }
    }

    public ValueTask<ExtensionPointRegistrySnapshot> GetBySurfaceAsync(
        ExtensionSurface surface,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            return ValueTask.FromResult(CreateSnapshotUnsafe(point => point.Surface == surface));
        }
    }

    public ValueTask<ExtensionPointDefinition?> FindByIdAsync(
        string extensionPointId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(extensionPointId))
        {
            return ValueTask.FromResult<ExtensionPointDefinition?>(null);
        }

        lock (_sync)
        {
            return _extensionPoints.TryGetValue(extensionPointId.Trim(), out var extensionPoint)
                ? ValueTask.FromResult<ExtensionPointDefinition?>(extensionPoint)
                : ValueTask.FromResult<ExtensionPointDefinition?>(null);
        }
    }

    private ExtensionPointRegistrySnapshot CreateSnapshotUnsafe(Func<ExtensionPointDefinition, bool> predicate)
    {
        var points = _extensionPoints.Values
            .Where(predicate)
            .OrderBy(x => x.ExtensionPointId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ExtensionPointRegistrySnapshot(_registryVersion, points);
    }
}
