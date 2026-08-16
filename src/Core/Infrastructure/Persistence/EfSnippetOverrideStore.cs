using Callora.Core.Application.Snippets;
using Callora.Core.Domain.Snippets;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Infrastructure.Persistence;

/// <inheritdoc />
public sealed class EfSnippetOverrideStore(HostPersistenceDbContext dbContext) : ISnippetOverrideStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<SnippetOverride>> ListAsync(
        IReadOnlyList<(string Scope, string ScopeKey)> scopeChain,
        IReadOnlyList<string> locales,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scopeChain);
        ArgumentNullException.ThrowIfNull(locales);

        if (scopeChain.Count == 0 || locales.Count == 0)
        {
            return [];
        }

        // Eine Abfrage für die ganze Kette statt einer je Ebene: Der Renderpfad fragt hier je
        // (Kette, Locale) genau einmal, und der Cache setzt an derselben Granularität an.
        var scopes = scopeChain.Select(entry => entry.Scope).Distinct().ToArray();
        var scopeKeys = scopeChain.Select(entry => entry.ScopeKey).Distinct().ToArray();

        var candidates = await dbContext.SnippetOverrides
            .AsNoTracking()
            .Where(entry =>
                scopes.Contains(entry.Scope)
                && scopeKeys.Contains(entry.ScopeKey)
                && locales.Contains(entry.Locale))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Das Kreuzprodukt aus Scopes und ScopeKeys kann Paare enthalten, die es in der Kette
        // nicht gibt (tenant/acme, wenn acme ein Workspace ist). Die Paarung wird deshalb hier
        // geprüft — ordinal, wie überall, wo ein ScopeKey verglichen wird.
        return
        [
            .. candidates.Where(entry => scopeChain.Any(scope =>
                scope.Scope == entry.Scope
                && string.Equals(scope.ScopeKey, entry.ScopeKey, StringComparison.Ordinal))),
        ];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SnippetOverride>> ListForScopeAsync(
        string scope,
        string scopeKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentNullException.ThrowIfNull(scopeKey);

        return await dbContext.SnippetOverrides
            .AsNoTracking()
            .Where(entry => entry.Scope == scope && entry.ScopeKey == scopeKey)
            .OrderBy(entry => entry.SnippetKey)
            .ThenBy(entry => entry.Locale)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(SnippetOverride entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var existing = await dbContext.SnippetOverrides
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.SnippetKey == entry.SnippetKey
                    && candidate.Locale == entry.Locale
                    && candidate.Scope == entry.Scope
                    && candidate.ScopeKey == entry.ScopeKey,
                cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            await dbContext.SnippetOverrides.AddAsync(entry, cancellationToken).ConfigureAwait(false);
            return;
        }

        existing.ChangeValue(entry.Value, entry.UpdatedBy, entry.UpdatedAtUtc);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(
        string snippetKey,
        string locale,
        string scope,
        string scopeKey,
        CancellationToken cancellationToken = default)
    {
        // Löschen heißt zurück zur Basis: Es gibt nichts wiederherzustellen, weil beim Anlegen
        // eines Geltungsbereichs nie etwas kopiert wurde (ADR-024 §3).
        await dbContext.SnippetOverrides
            .Where(entry =>
                entry.SnippetKey == snippetKey
                && entry.Locale == locale
                && entry.Scope == scope
                && entry.ScopeKey == scopeKey)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
