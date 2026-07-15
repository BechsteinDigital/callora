using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Calls;

namespace Callora.Plugin.Communication.Application.Flows;

/// <summary>Accepts the ringing inbound call of the triggering event.</summary>
public sealed class CallAcceptActionHandler(VoipCallHub callHub) : VoipCallFlowActionHandlerBase(callHub)
{
    public override string Type => "call.accept";

    protected override Task ExecuteOnCallAsync(
        ICall call,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken) =>
        call.AcceptAsync(cancellationToken);
}
