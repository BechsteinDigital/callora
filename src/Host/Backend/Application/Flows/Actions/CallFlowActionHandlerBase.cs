using Callora.Contracts.Communication;
using Callora.Host.Backend.Application.Communication.Calls;
using Callora.Host.PluginContracts.Application.Flows;

namespace Callora.Host.Backend.Application.Flows.Actions;

/// <summary>
/// Base for actions operating on the live call referenced by the event's
/// "callId" data field.
/// </summary>
public abstract class CallFlowActionHandlerBase(ActiveCallRegistry callRegistry) : IFlowActionHandler
{
    public abstract string Type { get; }

    public Task ExecuteAsync(
        RuleContext context,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        if (!context.Data.TryGetValue("callId", out var callId) ||
            string.IsNullOrWhiteSpace(context.WorkspaceKey) ||
            !callRegistry.TryGet(context.WorkspaceKey, callId, out var tracked) ||
            tracked is null)
        {
            throw new InvalidOperationException(
                $"Flow action '{Type}' requires a live call; call '{context.Data.GetValueOrDefault("callId")}' was not found.");
        }

        return ExecuteOnCallAsync(tracked.Call, parameters, cancellationToken);
    }

    protected abstract Task ExecuteOnCallAsync(
        ICall call,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken);
}
