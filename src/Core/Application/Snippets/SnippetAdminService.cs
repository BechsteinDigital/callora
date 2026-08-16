using Callora.Core.Application.Configuration;
using Callora.Core.Domain.Snippets;

namespace Callora.Core.Application.Snippets;

/// <summary>
/// Was ein Betreiber mit den Texten tun kann: ansehen, überschreiben, zurücknehmen (ADR-024 §5).
/// </summary>
/// <remarks>
/// Der Unterschied zu einer Lösung zur Bauzeit steht genau hier: Wer „Warenkorb" in „Bestellung"
/// ändern will, darf dafür kein Paket neu bauen müssen.
///
/// <para>
/// Gezeigt wird immer eine Ebene, nicht die aufgelöste Kette. Wer im Workspace steht, sieht, was
/// HIER gesetzt ist — nicht, was von Mandant oder global durchschlägt. Beides zu vermischen wäre
/// die Ansicht, in der niemand mehr sagen kann, was das Löschen einer Zeile bewirkt.
/// </para>
/// </remarks>
public sealed class SnippetAdminService(
    ISnippetBaseStore baseStore,
    ISnippetOverrideStore overrideStore,
    ISnippetCache cache)
{
    public async Task<IReadOnlyList<SnippetAdminEntry>> ListAsync(
        string locale,
        string scope,
        string scopeKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        EnsureKnownScope(scope);

        var basis = await baseStore.ListForLocaleAsync(locale.Trim(), cancellationToken).ConfigureAwait(false);
        var overrides = (await overrideStore
                .ListForScopeAsync(scope.Trim(), scopeKey?.Trim() ?? string.Empty, cancellationToken)
                .ConfigureAwait(false))
            .Where(entry => string.Equals(entry.Locale, locale.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToDictionary(entry => entry.SnippetKey, StringComparer.Ordinal);

        var entries = basis
            .Select(entry => new SnippetAdminEntry(
                entry.SnippetKey,
                entry.Locale,
                entry.PluginId,
                entry.Value,
                overrides.TryGetValue(entry.SnippetKey, out var over) ? over.Value : null))
            .ToList();

        // Verwaiste Abweichungen gehören dazu, nicht weggelassen: Ein Paket, das einen Schlüssel
        // aufgibt, macht die Arbeit des Betreibers unsichtbar — und unsichtbar ist der Zustand,
        // in dem sie später niemand mehr findet.
        var known = entries.Select(entry => entry.SnippetKey).ToHashSet(StringComparer.Ordinal);
        entries.AddRange(overrides.Values
            .Where(entry => !known.Contains(entry.SnippetKey))
            .Select(entry => new SnippetAdminEntry(
                entry.SnippetKey,
                entry.Locale,
                PluginId: string.Empty,
                BaseValue: null,
                OverrideValue: entry.Value)));

        return [.. entries.OrderBy(entry => entry.SnippetKey, StringComparer.Ordinal)];
    }

    public async Task SetAsync(
        string snippetKey,
        string locale,
        string scope,
        string scopeKey,
        string value,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        EnsureKnownScope(scope);

        await overrideStore
            .UpsertAsync(
                SnippetOverride.Create(snippetKey, locale, scope, scopeKey ?? string.Empty, value, updatedBy, DateTimeOffset.UtcNow),
                cancellationToken)
            .ConfigureAwait(false);

        Invalidate(scope, scopeKey);
    }

    /// <summary>
    /// Nimmt die Abweichung zurück — zurück zur Basis, ohne dass jemand die Basis kennen muss.
    /// </summary>
    public async Task ResetAsync(
        string snippetKey,
        string locale,
        string scope,
        string scopeKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snippetKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        EnsureKnownScope(scope);

        await overrideStore
            .RemoveAsync(snippetKey.Trim(), locale.Trim(), scope.Trim(), scopeKey?.Trim() ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);

        Invalidate(scope, scopeKey);
    }

    private void Invalidate(string scope, string? scopeKey)
    {
        // In der Granularität, in der geschrieben wurde: Ein globaler Eingriff liegt unter jeder
        // Kette, ein Mandant unter seinen Workspaces, ein Workspace nur unter sich.
        switch (scope.Trim())
        {
            case SystemConfigScopes.Workspace:
                cache.InvalidateWorkspace(scopeKey ?? string.Empty);
                break;
            case SystemConfigScopes.Tenant:
                cache.InvalidateTenant(scopeKey ?? string.Empty);
                break;
            default:
                cache.InvalidateAll();
                break;
        }
    }

    private static void EnsureKnownScope(string scope)
    {
        if (!SystemConfigScopes.IsValid(scope?.Trim()))
        {
            throw new ArgumentException(
                $"Unbekannter Geltungsbereich '{scope}'. Erlaubt sind global, tenant und workspace.",
                nameof(scope));
        }
    }
}
