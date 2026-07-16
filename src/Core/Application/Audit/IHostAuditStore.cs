using Callora.Core.Application.Audit;
using Callora.Core.Extensibility;

namespace Callora.Core.Application.Audit;

[CalloraInternal("Host audit store — Core-owned enforcement, not a plugin contract (REV2 §7.2)")]
public interface IHostAuditStore
{
    Task AppendAsync(HostAuditEntry entry, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HostAuditEntry>> GetRecentAsync(int take = 200, CancellationToken cancellationToken = default);
}
