using Callora.Contracts.Communication;
using Microsoft.Extensions.Logging;

namespace Callora.Host.Backend.Application.Communication.Calls;

/// <summary>
/// Graceful shutdown for the live-call stack (PLAT-234): hangs up every
/// remaining call with a bounded timeout and completes all SSE streams so
/// clients see a clean end-of-stream instead of an aborted connection.
/// </summary>
public sealed class CallGracefulShutdownService(
    ActiveCallRegistry callRegistry,
    CallEventBroadcaster broadcaster,
    ILogger<CallGracefulShutdownService> logger) : IHostedService
{
    private static readonly TimeSpan HangupTimeout = TimeSpan.FromSeconds(5);

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var trackedCalls = callRegistry.ListAllTracked();
        if (trackedCalls.Count > 0)
        {
            logger.LogInformation("Shutting down: hanging up {CallCount} active calls.", trackedCalls.Count);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(HangupTimeout);

            foreach (var tracked in trackedCalls)
            {
                try
                {
                    if (tracked.Call.State != CallState.Terminated)
                    {
                        await tracked.Call.HangupAsync(timeout.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception exception)
                {
                    logger.LogWarning(
                        exception,
                        "Hangup for call {CallId} during shutdown failed.",
                        tracked.Call.CallId);
                }
            }
        }

        // Nach den Hangups, damit die Ended-Events die Streams noch erreichen.
        broadcaster.CompleteAll();
    }
}
