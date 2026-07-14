using Callora.Contracts.Communication;
using Callora.Plugins.Voip.Application.Calls;

namespace Callora.Plugins.Voip.Application.Flows;

/// <summary>Rejects the ringing inbound call of the triggering event.</summary>
public sealed class CallRejectActionHandler(VoipCallHub callHub) : VoipCallFlowActionHandlerBase(callHub)
{
    public override string Type => "call.reject";

    protected override Task ExecuteOnCallAsync(
        ICall call,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken) =>
        call.RejectAsync(cancellationToken);
}
