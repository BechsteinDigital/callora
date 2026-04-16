using Callora.Host.Backend.Domain.Audit;

namespace Callora.Host.Backend.Application.Abstractions.Persistence;

public interface IPluginAuditLogRepository
{
    Task AddAsync(PluginAuditLog auditLog, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PluginAuditLog>> GetRecentAsync(int take, CancellationToken cancellationToken = default);
}
