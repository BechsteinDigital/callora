using Callora.Core.Application.Persistence;
using Callora.Core.Domain.Audit;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

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
