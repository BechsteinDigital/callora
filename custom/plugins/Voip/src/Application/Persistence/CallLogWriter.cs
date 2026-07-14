using Callora.Contracts.Communication;
using Callora.Host.PluginContracts.Application.Persistence;
using Microsoft.Extensions.Logging;

namespace Callora.Plugins.Voip.Application.Persistence;

/// <summary>
/// Persists ended calls as typed CallLog entities in the plugin database
/// (PLAT-260) — proves the plugin owns real EF data, not jsonb.
/// </summary>
public sealed class CallLogWriter(
    ICallEventStream eventStream,
    IPluginDbContextFactory<VoipDbContext> dbContextFactory,
    ILogger? logger = null) : IDisposable
{
    public void Attach() => eventStream.EventPublished += HandleCallEvent;

    public void Dispose() => eventStream.EventPublished -= HandleCallEvent;

    private void HandleCallEvent(CallStreamEvent callEvent)
    {
        if (callEvent.Type != CallEventTypes.Ended)
        {
            return;
        }

        _ = WriteAsync(callEvent.Call);
    }

    private async Task WriteAsync(CallSummary call)
    {
        try
        {
            await using var db = dbContextFactory.CreateDbContext();
            db.CallLogs.Add(new CallLog
            {
                Id = Guid.NewGuid(),
                WorkspaceKey = call.WorkspaceKey,
                CallId = call.CallId,
                ChannelId = call.ChannelId,
                Direction = call.Direction,
                TargetValue = call.TargetValue,
                TargetDisplayName = call.TargetDisplayName,
                StartedAtUtc = call.StartedAtUtc,
                EndedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "Persisting call log for {CallId} failed.", call.CallId);
        }
    }
}
