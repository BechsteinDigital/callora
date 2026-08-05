using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Extensibility;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Drops surface sessions past their expiry (ADR-017 §8.1). A session is never
/// extended by use, so an expired row is dead weight — without the purge the table
/// would grow with every login the platform ever served.
/// </summary>
[HostProtected]
public sealed class SurfaceSessionPurgeJobHandler(ISurfaceSessionStore store, TimeProvider timeProvider)
    : IBackgroundJobHandler
{
    public const string JobTypeName = "surfaces.session-purge";

    public string JobType => JobTypeName;

    public Task ExecuteAsync(
        BackgroundJobExecutionContext context,
        CancellationToken cancellationToken = default) =>
        store.PurgeExpiredAsync(timeProvider.GetUtcNow(), cancellationToken);
}
