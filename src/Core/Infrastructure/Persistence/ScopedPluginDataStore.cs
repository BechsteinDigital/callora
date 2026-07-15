using Callora.Host.PluginContracts.Application.Data;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// Singleton facade over the scoped EF-backed plugin data store. Creates one
/// service scope per operation so plugins can resolve <see cref="IPluginDataStore"/>
/// from the root provider without capturing a scoped DbContext.
/// </summary>
public sealed class ScopedPluginDataStore(IServiceScopeFactory scopeFactory) : IPluginDataStore
{
    public async Task<string?> GetAsync(PluginDataKey key, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        return await ResolveInner(scope).GetAsync(key, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetAsync(PluginDataKey key, string jsonDocument, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        await ResolveInner(scope).SetAsync(key, jsonDocument, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RemoveAsync(PluginDataKey key, CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        return await ResolveInner(scope).RemoveAsync(key, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PluginDataEntry>> ListAsync(
        PluginDataCollectionKey collection,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        return await ResolveInner(scope).ListAsync(collection, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListWorkspaceKeysAsync(
        string pluginId,
        string collection,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        return await ResolveInner(scope).ListWorkspaceKeysAsync(pluginId, collection, cancellationToken).ConfigureAwait(false);
    }

    private static EfPluginDataStore ResolveInner(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<EfPluginDataStore>();
}
