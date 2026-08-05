using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Flows;

/// <summary>
/// Flow action <c>call.hangup</c> — ends the call the triggering event names, whatever state it is
/// in. The one action that applies to an outbound call as well.
/// </summary>
public sealed class CallHangupActionHandler(ICallControlService callControl) : CallFlowActionHandlerBase(callControl)
{
    /// <inheritdoc />
    public override string Type => CallFlowActionTypes.Hangup;

    /// <inheritdoc />
    protected override Task<bool> ExecuteOnCallAsync(
        string workspaceKey,
        string callId,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken) =>
        CallControl.HangupAsync(workspaceKey, callId, cancellationToken);
}
