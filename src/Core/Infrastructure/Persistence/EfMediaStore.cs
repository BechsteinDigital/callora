using Callora.Core.Application.Media;
using Callora.Core.Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

public sealed class EfMediaStore(HostPersistenceDbContext dbContext) : IMediaStore
{
    public async Task<MediaItemSnapshot> AddAsync(
        string workspaceKey,
        string fileName,
        string contentType,
        long sizeBytes,
        string folder,
        string? createdBy,
        CancellationToken cancellationToken = default)
    {
        var entity = new MediaItem
        {
            Id = Guid.NewGuid(),
            WorkspaceKey = workspaceKey.Trim(),
            FileName = Path.GetFileName(fileName.Trim()),
            ContentType = contentType.Trim(),
            SizeBytes = sizeBytes,
            Folder = string.IsNullOrWhiteSpace(folder) ? "general" : folder.Trim(),
            CreatedBy = createdBy,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        dbContext.MediaItems.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToSnapshot(entity);
    }

    public async Task<IReadOnlyList<MediaItemSnapshot>> ListAsync(
        string workspaceKey,
        string? folder = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedWorkspace = workspaceKey.Trim();
        var query = dbContext.MediaItems
            .AsNoTracking()
            .Where(x => x.WorkspaceKey == normalizedWorkspace);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            var normalizedFolder = folder.Trim();
            query = query.Where(x => x.Folder == normalizedFolder);
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => ToSnapshot(x))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<MediaItemSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.MediaItems
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToSnapshot(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.MediaItems
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return false;
        }

        dbContext.MediaItems.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static MediaItemSnapshot ToSnapshot(MediaItem entity) => new(
        entity.Id,
        entity.WorkspaceKey,
        entity.FileName,
        entity.ContentType,
        entity.SizeBytes,
        entity.Folder,
        entity.CreatedBy,
        entity.CreatedAtUtc);
}
