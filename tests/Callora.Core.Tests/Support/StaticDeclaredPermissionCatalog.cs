using Callora.Core.Application.Security;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Manifest-declared permission keys without reading a manifest, for tests that care about
/// the inventory rather than about where the declarations came from.
/// </summary>
public sealed class StaticDeclaredPermissionCatalog(params string[] keys) : IPluginDeclaredPermissionCatalog
{
    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(keys);
}
