namespace Callora.Host.PluginContracts.Application.Media;

/// <summary>
/// Read access to workspace media assets for plugins — e.g. the voice plugin
/// streaming an announcement audio file into a call.
/// </summary>
public interface IMediaLibrary
{
    Task<IReadOnlyList<MediaAssetInfo>> ListAsync(
        string workspaceKey,
        string? folder = null,
        CancellationToken cancellationToken = default);

    /// <summary>Opens the asset bytes; null when the id is unknown.</summary>
    Task<Stream?> OpenReadAsync(Guid mediaId, CancellationToken cancellationToken = default);
}
