using Callora.Core.Application.Jobs.Contracts;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Purges finished call history past its retention window (#117).
/// <para>
/// Call logs carry <c>RemoteParty</c>, which is personal data. Deleting a workspace already
/// purges its records, but a workspace that stays active accumulated history forever, so the
/// only bound on how long a phone number was kept was how long the customer stayed.
/// </para>
/// <para>
/// The window is deployment-wide, from the plugin's own configuration. Per-workspace retention
/// policy is not implemented; a deployment that needs differing windows has to run separate
/// installations until it is.
/// </para>
/// </summary>
public sealed class CallLogRetentionJobHandler(
    ICallLogStore callLogStore,
    TimeProvider timeProvider,
    TimeSpan retention,
    ILogger<CallLogRetentionJobHandler> logger) : IBackgroundJobHandler
{
    /// <summary>Job type key this handler is registered under.</summary>
    public const string JobTypeName = "communication.call-log-retention";

    /// <inheritdoc />
    public string JobType => JobTypeName;

    /// <inheritdoc />
    public async Task ExecuteAsync(BackgroundJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Idempotent by construction: the cutoff is derived from the current time and only
        // finished calls are eligible, so a repeated run deletes nothing new.
        var cutoff = timeProvider.GetUtcNow() - retention;
        var purged = await callLogStore.PurgeEndedBeforeAsync(cutoff, cancellationToken).ConfigureAwait(false);
        if (purged > 0)
        {
            logger.LogInformation("Purged {Count} call log(s) that ended before {Cutoff}.", purged, cutoff);
        }
    }
}
