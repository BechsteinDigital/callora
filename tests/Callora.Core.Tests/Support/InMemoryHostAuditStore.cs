using Callora.Core.Application.Audit;

namespace Callora.Core.Tests.Support;

internal sealed class InMemoryHostAuditStore : IHostAuditStore
{
    private readonly List<HostAuditEntry> _entries = [];
    private readonly object _sync = new();

    public Task AppendAsync(HostAuditEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
            _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<HostAuditEntry>> GetRecentAsync(int take = 200, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
            return Task.FromResult<IReadOnlyList<HostAuditEntry>>(_entries.TakeLast(Math.Max(1, take)).ToArray());
    }
}
