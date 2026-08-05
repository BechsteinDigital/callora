using Callora.Core.Application.Jobs.Contracts;

namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>
/// Removes spent and expired media-stream tickets (#108). A ticket row is a lookup key
/// for a two-minute credential; keeping it after the credential died only grows the
/// table and widens the window in which a leaked row is worth analysing.
/// </summary>
public sealed class MediaStreamSessionPurgeJobHandler(IMediaStreamSessionStore sessionStore) : IBackgroundJobHandler
{
    /// <summary>Job type key this handler is registered under.</summary>
    public const string JobTypeName = "communication.media-session-purge";

    /// <inheritdoc />
    public string JobType => JobTypeName;

    /// <inheritdoc />
    public Task ExecuteAsync(BackgroundJobExecutionContext context, CancellationToken cancellationToken = default) =>
        sessionStore.PurgeExpiredAsync(
            DateTimeOffset.UtcNow,
            CommunicationStreamLimits.SessionRetention,
            cancellationToken);
}
