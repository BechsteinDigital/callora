using Callora.Core.Application.Audit;

namespace Callora.Core.Application.Audit;

public static class AuditStoreExtensions
{
    public static Task WritePluginAuditAsync(
        this IHostAuditStore auditStore,
        string action,
        string? pluginId,
        bool isSuccess,
        string? requestedBy,
        string? message,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default) =>
        auditStore.AppendAsync(
            new HostAuditEntry(
                DateTimeOffset.UtcNow,
                action,
                pluginId,
                isSuccess,
                requestedBy,
                message,
                metadata),
            cancellationToken);
}
