using Callora.Plugin.Communication.Abstractions;
using Callora.Plugins.Voip.Application.Calls;

namespace Callora.Plugins.Voip.Application.Flows;

/// <summary>Ends the call of the triggering event.</summary>
public sealed class CallHangupActionHandler(VoipCallHub callHub) : VoipCallFlowActionHandlerBase(callHub)
{
    public override string Type => "call.hangup";

    protected override Task ExecuteOnCallAsync(
        ICall call,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken) =>
        call.HangupAsync(cancellationToken);
}
