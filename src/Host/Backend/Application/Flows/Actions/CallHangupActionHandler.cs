using Callora.Contracts.Communication;
using Callora.Host.Backend.Application.Communication.Calls;

namespace Callora.Host.Backend.Application.Flows.Actions;

/// <summary>Ends the call of the triggering event.</summary>
public sealed class CallHangupActionHandler(ActiveCallRegistry callRegistry) : CallFlowActionHandlerBase(callRegistry)
{
    public override string Type => "call.hangup";

    protected override Task ExecuteOnCallAsync(
        ICall call,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken) =>
        call.HangupAsync(cancellationToken);
}
