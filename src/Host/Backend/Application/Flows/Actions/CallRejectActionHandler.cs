using Callora.Contracts.Communication;
using Callora.Host.Backend.Application.Communication.Calls;

namespace Callora.Host.Backend.Application.Flows.Actions;

/// <summary>Rejects the ringing inbound call of the triggering event.</summary>
public sealed class CallRejectActionHandler(ActiveCallRegistry callRegistry) : CallFlowActionHandlerBase(callRegistry)
{
    public override string Type => "call.reject";

    protected override Task ExecuteOnCallAsync(
        ICall call,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken) =>
        call.RejectAsync(cancellationToken);
}
