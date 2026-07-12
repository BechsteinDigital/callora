namespace Callora.Host.Backend.Application.Abstractions.Media;

public interface IMediaStore
{
    Task<MediaItemSnapshot> AddAsync(
        string workspaceKey,
        string fileName,
        string contentType,
        long sizeBytes,
        string folder,
        string? createdBy,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaItemSnapshot>> ListAsync(
        string workspaceKey,
        string? folder = null,
        CancellationToken cancellationToken = default);

    Task<MediaItemSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
