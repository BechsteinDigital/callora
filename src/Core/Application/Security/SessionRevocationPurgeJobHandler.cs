using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Extensibility;

namespace Callora.Core.Application.Security;

/// <summary>
/// Drops revocation entries whose tokens have expired (#105). Without it the
/// revocation list would grow with every logout forever, and the hot-path lookup
/// with it.
/// </summary>
[HostProtected]
public sealed class SessionRevocationPurgeJobHandler(IBackendSessionRevocationStore store) : IBackgroundJobHandler
{
    public const string JobTypeName = "security.session-revocation-purge";

    public string JobType => JobTypeName;

    public Task ExecuteAsync(BackgroundJobExecutionContext context, CancellationToken cancellationToken = default) =>
        store.PurgeExpiredAsync(cancellationToken);
}
