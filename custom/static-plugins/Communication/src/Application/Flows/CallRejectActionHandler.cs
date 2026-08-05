using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Flows;

/// <summary>
/// Flow action <c>call.reject</c> — turns away the ringing inbound call the triggering event names,
/// so a blocked caller hears a decision instead of ringing out.
/// </summary>
public sealed class CallRejectActionHandler(ICallControlService callControl) : CallFlowActionHandlerBase(callControl)
{
    /// <inheritdoc />
    public override string Type => CallFlowActionTypes.Reject;

    /// <inheritdoc />
    protected override Task<bool> ExecuteOnCallAsync(
        string workspaceKey,
        string callId,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken) =>
        CallControl.RejectAsync(workspaceKey, callId, cancellationToken);
}
