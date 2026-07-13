using Callora.Contracts.Communication;
using Callora.Host.Backend.Application.Communication.Calls;
using Callora.Host.Backend.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Host.Backend.Tests.Application.Communication;

public sealed class CallGracefulShutdownServiceTests
{
    [Fact]
    public async Task Stop_HangsUpActiveCalls_AndCompletesEventStreams()
    {
        var broadcaster = new CallEventBroadcaster();
        var registry = new ActiveCallRegistry(broadcaster);
        var call = new StaticCall(new CallTarget("+4930111"), CallDirection.Outbound, CallState.Connected);
        registry.TrackPlaced("workspace-a", "voice-1", call);
        using var subscription = broadcaster.Subscribe("workspace-a");

        var service = new CallGracefulShutdownService(
            registry,
            broadcaster,
            NullLogger<CallGracefulShutdownService>.Instance);

        await service.StopAsync(CancellationToken.None);

        Assert.Equal(CallState.Terminated, call.State);
        Assert.Empty(registry.ListAllTracked());

        // Der Stream endet sauber: ReadAllAsync läuft leer statt zu blockieren.
        var remaining = new List<CallEvent>();
        await foreach (var callEvent in subscription.Reader.ReadAllAsync())
        {
            remaining.Add(callEvent);
        }

        Assert.Contains(remaining, x => x.Type == CallEventTypes.Ended);
    }
}
