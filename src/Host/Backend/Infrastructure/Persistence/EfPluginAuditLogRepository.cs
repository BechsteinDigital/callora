using Callora.Host.Backend.Application.Abstractions.Persistence;
using Callora.Host.Backend.Domain.Audit;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

public sealed class EfPluginAuditLogRepository(HostPersistenceDbContext dbContext) : IPluginAuditLogRepository
{
    public Task AddAsync(PluginAuditLog auditLog, CancellationToken cancellationToken = default) =>
        dbContext.PluginAuditLogs.AddAsync(auditLog, cancellationToken).AsTask();

    public async Task<IReadOnlyList<PluginAuditLog>> GetRecentAsync(
        int take,
        CancellationToken cancellationToken = default) =>
        await dbContext.PluginAuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
