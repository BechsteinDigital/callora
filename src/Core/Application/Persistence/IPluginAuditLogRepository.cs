using Callora.Core.Domain.Audit;

namespace Callora.Core.Application.Persistence;

public interface IPluginAuditLogRepository
{
    Task AddAsync(PluginAuditLog auditLog, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PluginAuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
}
