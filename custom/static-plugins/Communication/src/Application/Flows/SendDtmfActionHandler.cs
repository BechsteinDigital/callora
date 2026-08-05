using Callora.Plugin.Communication.Abstractions;

namespace Callora.Plugin.Communication.Application.Flows;

/// <summary>
/// Flow action <c>call.dtmf</c> — sends the configured tones on the call the triggering event names,
/// for example to punch an extension into a carrier's menu after an outbound call connects.
/// </summary>
public sealed class SendDtmfActionHandler(ICallControlService callControl) : CallFlowActionHandlerBase(callControl)
{
    /// <summary>Action parameter carrying the tones to send.</summary>
    public const string TonesParameter = "tones";

    /// <inheritdoc />
    public override string Type => CallFlowActionTypes.SendDtmf;

    /// <inheritdoc />
    protected override Task<bool> ExecuteOnCallAsync(
        string workspaceKey,
        string callId,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        // A misconfigured action is a flow-authoring error, reported before anything is sent rather
        // than as a partially dialled sequence.
        if (!parameters.TryGetValue(TonesParameter, out var tones) || string.IsNullOrWhiteSpace(tones))
        {
            throw new InvalidOperationException(
                $"Flow action '{Type}' requires a '{TonesParameter}' parameter.");
        }

        return CallControl.SendDtmfAsync(workspaceKey, callId, tones, cancellationToken);
    }
}
