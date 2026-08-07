using Callora.Core.Application.Extensions;
using Callora.Core.Domain.Extensions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Callora.Core.Infrastructure.Persistence;

public sealed class EfWorkspaceSectionLayoutStore(HostPersistenceDbContext dbContext)
    : IWorkspaceSectionLayoutStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ThemeSectionLayouts> ListAsync(
        string pluginId,
        string version,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        var normalizedPluginId = pluginId.Trim();
        var normalizedVersion = version.Trim();

        var rows = await dbContext.WorkspaceSectionLayoutDefinitions
            .AsNoTracking()
            .Where(x => x.PluginId == normalizedPluginId && x.Version == normalizedVersion && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.LayoutKey)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        // Ein Theme ohne eigene Layouts hat keine Zeile — und erbt.
        return new ThemeSectionLayouts(
            rows.Select(Read).ToArray(),
            rows.Length == 0 || rows[0].InheritsBase);
    }

    public async Task ReplaceForPluginAsync(
        string pluginId,
        string version,
        IReadOnlyList<SectionLayoutDefinition> layouts,
        bool inheritsBase,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentNullException.ThrowIfNull(layouts);

        var normalizedPluginId = pluginId.Trim();
        var normalizedVersion = version.Trim();
        var nowUtc = DateTimeOffset.UtcNow;

        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await dbContext.WorkspaceSectionLayoutDefinitions
            .Where(x => x.PluginId == normalizedPluginId && x.Version == normalizedVersion)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var entities = layouts
            .Where(layout => !string.IsNullOrWhiteSpace(layout.LayoutKey))
            .Select(layout => new WorkspaceSectionLayoutDefinition
            {
                Id = Guid.NewGuid(),
                LayoutKey = layout.LayoutKey.Trim(),
                PluginId = normalizedPluginId,
                Version = normalizedVersion,
                Label = string.IsNullOrWhiteSpace(layout.Label) ? layout.LayoutKey.Trim() : layout.Label.Trim(),
                RegionsJson = JsonSerializer.Serialize(layout.Regions, Json),
                SortOrder = layout.SortOrder,
                InheritsBase = inheritsBase,
                IsActive = true,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            })
            .ToArray();

        if (entities.Length > 0)
        {
            await dbContext.WorkspaceSectionLayoutDefinitions
                .AddRangeAsync(entities, cancellationToken)
                .ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearForPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        var normalizedPluginId = pluginId.Trim();
        await dbContext.WorkspaceSectionLayoutDefinitions
            .Where(x => x.PluginId == normalizedPluginId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static SectionLayoutDefinition Read(WorkspaceSectionLayoutDefinition row)
    {
        // Ein Layout mit unlesbaren Regionen wird zu einem Layout OHNE Regionen, nicht zu einem
        // Fehler: Der Editor bietet es dann als leer an, statt dass die ganze Liste ausfällt und
        // niemand mehr irgendein Layout wählen kann.
        SectionLayoutRegion[]? regions = null;
        try
        {
            regions = JsonSerializer.Deserialize<SectionLayoutRegion[]>(row.RegionsJson, Json);
        }
        catch (JsonException)
        {
            regions = null;
        }

        return new SectionLayoutDefinition(
            row.LayoutKey,
            row.Label,
            regions ?? [],
            row.SortOrder);
    }
}
