using Callora.Core.Application.Flows.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Calls;

namespace Callora.Plugin.Communication.Application.Flows;

/// <summary>
/// Base for flow actions operating on the live call referenced by the
/// event's "callId" data field. Exported by the voice plugin (PLAT-257).
/// </summary>
public abstract class VoipCallFlowActionHandlerBase(VoipCallHub callHub) : IFlowActionHandler
{
    public abstract string Type { get; }

    public Task ExecuteAsync(
        RuleContext context,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        if (!context.Data.TryGetValue("callId", out var callId) ||
            string.IsNullOrWhiteSpace(context.WorkspaceKey) ||
            !callHub.TryGet(context.WorkspaceKey, callId, out var call) ||
            call is null)
        {
            throw new InvalidOperationException(
                $"Flow action '{Type}' requires a live call; call '{context.Data.GetValueOrDefault("callId")}' was not found.");
        }

        return ExecuteOnCallAsync(call, parameters, cancellationToken);
    }

    protected abstract Task ExecuteOnCallAsync(
        ICall call,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken);
}
