using Callora.Contracts.Communication;
using Callora.Host.Backend.Application.Communication.Calls;

namespace Callora.Host.Backend.Application.Flows.Actions;

/// <summary>Accepts the ringing inbound call of the triggering event.</summary>
public sealed class CallAcceptActionHandler(ActiveCallRegistry callRegistry) : CallFlowActionHandlerBase(callRegistry)
{
    public override string Type => "call.accept";

    protected override Task ExecuteOnCallAsync(
        ICall call,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken) =>
        call.AcceptAsync(cancellationToken);
}
