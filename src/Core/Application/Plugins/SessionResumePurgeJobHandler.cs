using Callora.Core.Application.Jobs.Contracts;
using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Drops resume promises past their expiry (ADR-018 §2.2). A ticket lives for minutes and is never
/// extended by use, so an expired row is dead weight — without the purge the table would grow with
/// every real-time session the platform ever served.
/// </summary>
[HostProtected]
public sealed class SessionResumePurgeJobHandler(
    ISessionResumeTicketStore tickets,
    TimeProvider timeProvider)
    : IBackgroundJobHandler
{
    public const string JobTypeName = "plugins.session-resume-purge";

    public string JobType => JobTypeName;

    public async Task ExecuteAsync(
        BackgroundJobExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        await tickets
            .PurgeExpiredAsync(timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
    }
}
