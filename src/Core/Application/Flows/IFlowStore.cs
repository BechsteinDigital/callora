namespace Callora.Core.Application.Flows;

public interface IFlowStore
{
    Task<IReadOnlyList<FlowSnapshot>> ListAsync(
        string? workspaceKey = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FlowSnapshot>> ListActiveForTriggerAsync(
        string triggerEvent,
        string workspaceKey,
        CancellationToken cancellationToken = default);

    Task<FlowSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<FlowSnapshot> UpsertAsync(FlowSnapshot flow, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
