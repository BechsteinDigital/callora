using System.Text.Json;
using Callora.Host.PluginContracts.Application.Jobs;

namespace Callora.Host.Backend.Application.Entitlements;

/// <summary>
/// Processes marketplace entitlement events from the durable job queue.
/// Failed events are retried with backoff; exhausted jobs remain visible as
/// dead letters under /api/jobs.
/// </summary>
public sealed class MarketplaceEntitlementSyncJobHandler(MarketplaceEntitlementApplier applier) : IBackgroundJobHandler
{
    public const string JobTypeName = "marketplace.entitlement-sync";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string JobType => JobTypeName;

    public async Task ExecuteAsync(BackgroundJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Deserialize<MarketplaceEntitlementEventPayload>(context.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Marketplace entitlement event payload is empty.");

        await applier.ApplyAsync(payload, cancellationToken).ConfigureAwait(false);
    }
}
