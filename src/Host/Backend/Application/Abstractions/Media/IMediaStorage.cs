namespace Callora.Host.Backend.Application.Abstractions.Media;

/// <summary>
/// Byte storage for media assets, addressed exclusively by media id so no
/// caller-supplied path ever reaches the file system.
/// </summary>
public interface IMediaStorage
{
    Task WriteAsync(Guid mediaId, Stream content, CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(Guid mediaId, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid mediaId, CancellationToken cancellationToken = default);
}
