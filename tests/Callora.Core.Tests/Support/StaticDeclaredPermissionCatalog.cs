using Callora.Core.Application.Security;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Manifest-declared permission keys without reading a manifest, for tests that care about
/// the inventory rather than about where the declarations came from.
/// </summary>
public sealed class StaticDeclaredPermissionCatalog(params string[] keys) : IPluginDeclaredPermissionCatalog
{
    /// <summary>Which plugin declared them — one made-up owner, because these tests do not care.</summary>
    public string PluginId { get; init; } = "test-plugin";

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(keys);

    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ListByPluginAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(
            keys.Length == 0
                ? new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
                : new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
                {
                    [PluginId] = keys
                });
}
