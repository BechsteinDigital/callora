using Callora.Host.Backend.Application.Abstractions.Integrations;
using Callora.Host.Backend.Domain.Integrations;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

/// <summary>
/// EF-backed store for named machine-to-machine integrations (PLAT-264).
/// </summary>
public sealed class EfIntegrationCredentialStore(HostPersistenceDbContext dbContext)
    : IIntegrationCredentialStore
{
    public async Task<IntegrationCredential?> FindActiveByKeyHashAsync(
        string keyHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyHash);

        return await dbContext.IntegrationCredentials
            .AsNoTracking()
            .SingleOrDefaultAsync(
                c => c.KeyHash == keyHash && c.IsActive && c.RevokedAtUtc == null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<IntegrationCredential>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.IntegrationCredentials
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IntegrationCredential?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.IntegrationCredentials
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();

        return await dbContext.IntegrationCredentials
            .AnyAsync(c => c.Name == normalized, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(IntegrationCredential credential, CancellationToken cancellationToken = default)
    {
        dbContext.IntegrationCredentials.Add(credential);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RevokeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await dbContext.IntegrationCredentials
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (row is null || !row.IsActive)
            return false;

        row.IsActive = false;
        row.RevokedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
