using Microsoft.Extensions.Caching.Memory;

namespace Callora.Core.Application.Extensions;

/// <summary>
/// Legt das aufgelöste Theme ab, bis jemand daran schreibt.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="WorkspacePublicThemeResolver"/> fragt pro Aufruf sechsmal die Datenbank — Workspace,
/// Fläche, Definitionen, Werte auf Workspace- und Flächenebene, Sektionslayouts. Im öffentlichen
/// Renderpfad passiert das bei jeder Anfrage, für Daten, die ein Betreiber im Zweifel einmal pro
/// Woche anfasst.
/// </para>
/// <para>
/// Anders als bei der Flächentabelle sind hier Schlüssel und Wert unproblematisch: Der Schlüssel
/// ist (Workspace, Fläche) — beides stammt aus der Datenbank, seine Menge ist also begrenzt und
/// nicht vom Anfragenden bestimmbar. Und <see cref="WorkspacePublicTheme"/> ist ein Record aus
/// bereits fertig gerechneten Werten; niemand kann ihn versehentlich für alle anderen verändern.
/// </para>
/// <para>
/// Verworfen wird pauschal, nicht je Schlüssel. Ein Theme-Wert kann über die Vererbung auf
/// beliebig viele Flächen wirken — welche genau, wüsste diese Klasse nur, wenn sie die
/// Vererbungsregeln ein zweites Mal nachbaute. Zwei Fassungen derselben Regel wären zwei
/// Gelegenheiten, sie unterschiedlich zu meinen; ein pauschaler Wurf ist die billigere Wahrheit,
/// weil geschrieben selten und gelesen ständig wird.
/// </para>
/// </remarks>
public sealed class CachedWorkspacePublicThemeResolver(
    IMemoryCache cache,
    IServiceScopeFactory scopeFactory) : IWorkspacePublicThemeResolver, IThemeResolutionCache
{
    /// <summary>
    /// Rückfallebene für eine vergessene Invalidierung oder den Schreibvorgang einer zweiten
    /// Instanz — nicht das Mittel, mit dem Aktualität hergestellt wird.
    /// </summary>
    private static readonly TimeSpan Fallback = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Der Generationszähler steht IM Schlüssel, statt dass Einträge einzeln entfernt würden.
    /// <para>
    /// Ein <c>IMemoryCache</c> kann seine Schlüssel nicht aufzählen; ohne mitgeführten Index
    /// ließe sich pauschal gar nicht räumen. Den Zähler zu erhöhen macht jeden bisherigen
    /// Schlüssel unerreichbar — die alten Einträge verfallen von selbst, statt dass jemand Buch
    /// über sie führen muss.
    /// </para>
    /// </summary>
    private long _generation;

    /// <inheritdoc />
    public Task<WorkspacePublicTheme?> ResolveAsync(
        string workspaceKey,
        CancellationToken cancellationToken = default) =>
        ResolveForSurfaceAsync(workspaceKey, surfaceKey: null, cancellationToken);

    /// <inheritdoc />
    public Task<WorkspacePublicTheme?> ResolveForSurfaceAsync(
        string workspaceKey,
        string? surfaceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);

        var normalizedWorkspaceKey = workspaceKey.Trim();
        var normalizedSurfaceKey = string.IsNullOrWhiteSpace(surfaceKey) ? string.Empty : surfaceKey.Trim();
        var cacheKey = BuildCacheKey(normalizedWorkspaceKey, normalizedSurfaceKey);

        return cache.TryGetValue(cacheKey, out WorkspacePublicTheme? cached)
            ? Task.FromResult(cached)
            : ResolveAndCacheAsync(cacheKey, normalizedWorkspaceKey, normalizedSurfaceKey, cancellationToken);
    }

    /// <inheritdoc />
    public void Invalidate() => Interlocked.Increment(ref _generation);

    private async Task<WorkspacePublicTheme?> ResolveAndCacheAsync(
        string cacheKey,
        string workspaceKey,
        string surfaceKey,
        CancellationToken cancellationToken)
    {
        // Eigener Scope: Der Dienst lebt für den Prozess, der dekorierte Resolver hängt an
        // scoped Stores. Ihn festzuhalten hieße, deren DbContext über die Anfrage hinaus zu binden.
        using var scope = scopeFactory.CreateScope();
        var inner = scope.ServiceProvider.GetRequiredService<WorkspacePublicThemeResolver>();

        var resolved = await inner
            .ResolveForSurfaceAsync(
                workspaceKey,
                string.IsNullOrEmpty(surfaceKey) ? null : surfaceKey,
                cancellationToken)
            .ConfigureAwait(false);

        // Auch das Nichts wird abgelegt: Ein Workspace ohne Theme ist der Normalfall, und ihn
        // nicht zu cachen hieße, genau dort sechs Abfragen je Anfrage zu behalten, wo es nichts
        // zu holen gibt.
        cache.Set(cacheKey, resolved, Fallback);
        return resolved;
    }

    private string BuildCacheKey(string workspaceKey, string surfaceKey) =>
        $"workspace-public-theme:{Interlocked.Read(ref _generation)}:{workspaceKey}:{surfaceKey}";
}
