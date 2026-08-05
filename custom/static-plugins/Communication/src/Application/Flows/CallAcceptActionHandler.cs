using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Flows;

/// <summary>
/// Flow action <c>call.accept</c> — answers the ringing inbound call the triggering event names.
/// Typically wired to <c>call.ringing</c> behind a condition, so a rule can pick up calls from a
/// known number automatically.
/// </summary>
public sealed class CallAcceptActionHandler(ICallControlService callControl) : CallFlowActionHandlerBase(callControl)
{
    /// <inheritdoc />
    public override string Type => CallFlowActionTypes.Accept;

    /// <inheritdoc />
    protected override Task<bool> ExecuteOnCallAsync(
        string workspaceKey,
        string callId,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken) =>
        CallControl.AcceptAsync(workspaceKey, callId, cancellationToken);
}
