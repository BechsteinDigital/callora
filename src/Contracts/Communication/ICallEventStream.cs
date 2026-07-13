namespace Callora.Contracts.Communication;

/// <summary>
/// Live call events exported by communication plugins (PLAT-257). Platform
/// rails (SSE facade, flow triggers, webhook relays) consume this contract
/// instead of holding call logic themselves.
/// </summary>
public interface ICallEventStream
{
    /// <summary>
    /// Raised for every published event regardless of workspace —
    /// platform-internal consumers (flows, webhooks) attach here.
    /// </summary>
    event Action<CallStreamEvent>? EventPublished;

    /// <summary>Opens one buffered, workspace-scoped subscription.</summary>
    ICallEventSubscription Subscribe(string workspaceKey);
}
