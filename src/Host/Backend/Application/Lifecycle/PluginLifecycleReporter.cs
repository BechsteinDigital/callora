using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Application.Abstractions.Events;
using Callora.Host.Backend.Application.Audit;
using Callora.Host.Backend.Application.Events;

namespace Callora.Host.Backend.Application.Lifecycle;

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
        string action,
        string? pluginId,
        bool isSuccess,
        string? requestedBy,
        string? message,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        await WriteAuditAsync(action, pluginId, isSuccess, requestedBy, message, metadata, cancellationToken)
            .ConfigureAwait(false);
        await PublishEventAsync(action, pluginId, isSuccess, requestedBy, message, metadata, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Writes one audit entry without publishing an event.
    /// </summary>
    public Task WriteAuditAsync(
        string action,
        string? pluginId,
        bool isSuccess,
        string? requestedBy,
        string? message,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveMetadata = EnrichMetadataWithCorrelationId(metadata);
        return auditStore.WritePluginAuditAsync(
            action,
            pluginId,
            isSuccess,
            requestedBy,
            message,
            effectiveMetadata,
            cancellationToken);
    }

    /// <summary>
    /// Publishes one lifecycle event without writing an audit entry.
    /// </summary>
    public Task PublishEventAsync(
        string action,
        string? pluginId,
        bool isSuccess,
        string? requestedBy,
        string? message,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveMetadata = EnrichMetadataWithCorrelationId(metadata);
        return eventPublisher.PublishAsync(
            new PluginLifecycleChangedEvent(
                DateTimeOffset.UtcNow,
                action,
                pluginId,
                isSuccess,
                requestedBy,
                message,
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
            action: "plugin.install",
            pluginId: pluginId,
            isSuccess: false,
            requestedBy: requestedBy,
            message: message,
            metadata: metadata,
            cancellationToken: cancellationToken);
    }

    private static IReadOnlyDictionary<string, string>? EnrichMetadataWithCorrelationId(
        IReadOnlyDictionary<string, string>? metadata)
    {
        var correlationId = PluginLifecycleTelemetry.GetCurrentCorrelationId();
        if (string.IsNullOrWhiteSpace(correlationId))
            return metadata;

        var enriched = metadata is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(metadata, StringComparer.Ordinal);
        enriched["correlationId"] = correlationId;
        return enriched;
    }
}
