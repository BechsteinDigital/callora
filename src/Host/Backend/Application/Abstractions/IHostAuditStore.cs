using Callora.Host.Backend.Application.Audit;

namespace Callora.Host.Backend.Application.Abstractions;

public interface IHostAuditStore
{
    Task AppendAsync(HostAuditEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HostAuditEntry>> GetRecentAsync(int take = 200, CancellationToken cancellationToken = default);
}
