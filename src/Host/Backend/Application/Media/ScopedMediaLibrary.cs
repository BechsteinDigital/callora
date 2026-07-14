using Callora.Host.Backend.Application.Media;
using Callora.Host.PluginContracts.Application.Media;

namespace Callora.Host.Backend.Application.Media;

/// <summary>
/// Singleton media library facade for plugins over the scoped media store.
/// </summary>
public sealed class ScopedMediaLibrary(
    IServiceScopeFactory scopeFactory,
    IMediaStorage storage) : IMediaLibrary
{
    public async Task<IReadOnlyList<MediaAssetInfo>> ListAsync(
        string workspaceKey,
        string? folder = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IMediaStore>();
        var items = await store.ListAsync(workspaceKey, folder, cancellationToken).ConfigureAwait(false);
        return items
            .Select(item => new MediaAssetInfo(
                item.Id,
                item.WorkspaceKey,
                item.FileName,
                item.ContentType,
                item.SizeBytes,
                item.Folder))
            .ToArray();
    }

    public Task<Stream?> OpenReadAsync(Guid mediaId, CancellationToken cancellationToken = default) =>
        storage.OpenReadAsync(mediaId, cancellationToken);
}
