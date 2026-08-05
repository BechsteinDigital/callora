using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Extensibility;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Drops surface transport state past its expiry (ADR-017 §8). A session is never
/// extended by use and a handoff ticket lives for seconds, so an expired row of
/// either kind is dead weight — without the purge both tables would grow with every
/// login and every handover the platform ever served.
/// </summary>
[HostProtected]
public sealed class SurfaceSessionPurgeJobHandler(
    ISurfaceSessionStore sessions,
    ISurfaceHandoffTicketStore tickets,
    TimeProvider timeProvider)
    : IBackgroundJobHandler
{
    public const string JobTypeName = "surfaces.session-purge";

    public string JobType => JobTypeName;

    public async Task ExecuteAsync(
        BackgroundJobExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = timeProvider.GetUtcNow();
        await sessions.PurgeExpiredAsync(nowUtc, cancellationToken).ConfigureAwait(false);
        await tickets.PurgeExpiredAsync(nowUtc, cancellationToken).ConfigureAwait(false);
    }
}
