using System.Text.Json;
using Callora.Core.Application.Persistence;
using Callora.Core.Application.Audit;
using Callora.Core.Domain.Audit;

namespace Callora.Core.Infrastructure.Persistence;

public sealed class DatabaseHostAuditStore(
    IPluginAuditLogRepository repository,
    IHostUnitOfWork unitOfWork) : IHostAuditStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task AppendAsync(HostAuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var model = new PluginAuditLog
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = entry.OccurredAtUtc,
            Action = entry.Action,
            PluginId = entry.PluginId,
            IsSuccess = entry.IsSuccess,
            RequestedBy = entry.RequestedBy,
            Message = entry.Message,
            MetadataJson = entry.Metadata is null
                ? null
                : JsonSerializer.Serialize(entry.Metadata, JsonOptions)
        };

        await repository.AddAsync(model, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<HostAuditEntry>> GetRecentAsync(
        int take = 200,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 2_000);
        var rows = await repository.GetRecentAsync(take, cancellationToken).ConfigureAwait(false);

        var result = new List<HostAuditEntry>(rows.Count);
        foreach (var row in rows)
        {
            IReadOnlyDictionary<string, string>? metadata = null;
            if (!string.IsNullOrWhiteSpace(row.MetadataJson))
            {
                metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(row.MetadataJson, JsonOptions);
            }

            result.Add(new HostAuditEntry(
                row.OccurredAtUtc,
                row.Action,
                row.PluginId,
                row.IsSuccess,
                row.RequestedBy,
                row.Message,
                metadata));
        }

        return result;
    }
}
