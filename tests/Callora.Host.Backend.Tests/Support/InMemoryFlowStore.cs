using Callora.Host.Backend.Application.Abstractions.Flows;

namespace Callora.Host.Backend.Tests.Support;

/// <summary>
/// In-memory flow store for trigger and job-handler tests.
/// </summary>
public sealed class InMemoryFlowStore : IFlowStore
{
    private readonly List<FlowSnapshot> _flows = [];

    public Task<IReadOnlyList<FlowSnapshot>> ListAsync(
        string? workspaceKey = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FlowSnapshot> result = _flows
            .Where(flow => workspaceKey is null ||
                string.Equals(flow.WorkspaceKey, workspaceKey.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<FlowSnapshot>> ListActiveForTriggerAsync(
        string triggerEvent,
        string workspaceKey,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<FlowSnapshot> result = _flows
            .Where(flow => flow.IsActive &&
                string.Equals(flow.TriggerEvent, triggerEvent, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(flow.WorkspaceKey, workspaceKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(flow => flow.Priority)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<FlowSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_flows.FirstOrDefault(flow => flow.Id == id));

    public Task<FlowSnapshot> UpsertAsync(FlowSnapshot flow, CancellationToken cancellationToken = default)
    {
        var stored = flow.Id == Guid.Empty ? flow with { Id = Guid.NewGuid() } : flow;
        _flows.RemoveAll(existing => existing.Id == stored.Id);
        _flows.Add(stored);
        return Task.FromResult(stored);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_flows.RemoveAll(flow => flow.Id == id) > 0);
}
