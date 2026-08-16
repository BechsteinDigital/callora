using Callora.Core.Application.Snippets;
using Callora.Core.Domain.Snippets;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// Die Basis in der Datenbank: geschrieben beim Installieren und Aktualisieren, gelesen vom
/// Resolver (ADR-024 §4).
/// </summary>
/// <remarks>
/// Ein Typ für beide Seiten, weil es dieselbe Tabelle ist — aber zwei Verträge, weil die
/// Aufrufer verschieden sind: Der Sync schreibt paketweise, der Renderpfad liest je Locale über
/// alle Pakete.
/// </remarks>
public sealed class EfSnippetBaseStore(HostPersistenceDbContext dbContext)
    : ISnippetBaseStore, ISnippetBaseSource
{
    /// <inheritdoc />
    public async ValueTask<IReadOnlyDictionary<string, string>> GetAsync(
        string locale,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var entries = await dbContext.SnippetBase
            .AsNoTracking()
            .Where(entry => entry.Locale == locale)
            .Select(entry => new { entry.SnippetKey, entry.Value })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var snippets = new Dictionary<string, string>(entries.Count, StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            snippets[entry.SnippetKey] = entry.Value;
        }

        return snippets;
    }

    /// <inheritdoc />
    public async Task ReplaceForPluginAsync(
        string pluginId,
        IReadOnlyList<SnippetBaseEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentNullException.ThrowIfNull(entries);

        await ClearForPluginAsync(pluginId, cancellationToken).ConfigureAwait(false);
        if (entries.Count > 0)
        {
            await dbContext.SnippetBase.AddRangeAsync(entries, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task ClearForPluginAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        // Nur die Basis. Die Abweichungen des Betreibers liegen in einer anderen Tabelle und
        // bleiben stehen — ein Wiedereinspielen des Plugins stellt sie damit ohne Zutun wieder her.
        return dbContext.SnippetBase
            .Where(entry => entry.PluginId == pluginId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
