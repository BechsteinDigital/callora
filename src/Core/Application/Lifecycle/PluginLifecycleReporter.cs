using Callora.Core.Application.Audit;
using Callora.Core.Application.Events;

namespace Callora.Core.Application.Lifecycle;

/// <summary>
/// Writes audit entries and publishes lifecycle events for plugin operations.
/// </summary>
public sealed class PluginLifecycleReporter(
    IHostAuditStore auditStore,
    IHostApplicationEventPublisher eventPublisher)
{
    /// <summary>
    /// Writes one audit entry and publishes one lifecycle event with identical payloads.
    /// </summary>
    public async Task ReportAsync(
        PluginLifecycleReport report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        await WriteAuditAsync(report, cancellationToken).ConfigureAwait(false);
        await PublishEventAsync(report, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes one audit entry without publishing an event.
    /// </summary>
    public Task WriteAuditAsync(
        PluginLifecycleReport report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        var effectiveMetadata = EnrichMetadataWithCorrelationId(report.Metadata);
        return auditStore.WritePluginAuditAsync(
            report.Action,
            report.PluginId,
            report.IsSuccess,
            report.RequestedBy,
            report.Message,
            effectiveMetadata,
            cancellationToken);
    }

    /// <summary>
    /// Publishes one lifecycle event without writing an audit entry.
    /// </summary>
    public Task PublishEventAsync(
        PluginLifecycleReport report,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        var effectiveMetadata = EnrichMetadataWithCorrelationId(report.Metadata);
        return eventPublisher.PublishAsync(
            new PluginLifecycleChangedEvent(
                DateTimeOffset.UtcNow,
                report.Action,
                report.PluginId,
                report.IsSuccess,
                report.RequestedBy,
                report.Message,
                effectiveMetadata),
            cancellationToken);
    }

    /// <summary>
    /// Reports one rejected install gate (registry, signature, extension wiring) as audit entry plus event.
    /// </summary>
    public Task ReportInstallGateRejectAsync(
        string? pluginId,
        string? requestedBy,
        string? message,
        string gateType,
        string reasonCode,
        string assemblyPath,
        IReadOnlyDictionary<string, string>? additionalMetadata,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, string>
        {
            ["gateType"] = gateType,
            ["reasonCode"] = reasonCode,
            ["assemblyPath"] = assemblyPath
        };

        if (additionalMetadata is not null)
        {
            foreach (var (key, value) in additionalMetadata)
            {
                metadata[key] = value;
            }
        }

        return ReportAsync(
            new PluginLifecycleReport(
                Action: "plugin.install",
                PluginId: pluginId,
                IsSuccess: false,
                RequestedBy: requestedBy,
                Message: message,
                Metadata: metadata),
            cancellationToken);
    }

    private static IReadOnlyDictionary<string, string>? EnrichMetadataWithCorrelationId(
        IReadOnlyDictionary<string, string>? metadata)
    {
        var correlationId = PluginLifecycleTelemetry.GetCurrentCorrelationId();
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return metadata;
        }

        var enriched = metadata is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(metadata, StringComparer.Ordinal);
        enriched["correlationId"] = correlationId;
        return enriched;
    }
}
