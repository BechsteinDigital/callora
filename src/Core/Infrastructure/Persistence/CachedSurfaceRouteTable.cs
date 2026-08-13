using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Callora.Core.Infrastructure.Persistence;

/// <summary>
/// Lädt die Flächentabelle einmal und hält sie, bis jemand schreibt.
/// </summary>
/// <remarks>
/// <para>
/// Vorher las der öffentliche Renderpfad diese Zeilen bei jeder Anfrage — nach dem Entfernen der
/// doppelten Abfrage einmal statt zweimal, aber immer noch einmal zu oft für Daten, die sich
/// zwischen zwei Deployments kaum ändern.
/// </para>
/// <para>
/// <b>Warum die Entitäten selbst im Cache liegen und keine Projektion.</b> Alles, was das Matching
/// und <see cref="EffectiveSurface"/> brauchen, ist praktisch die ganze Zeile — eine Projektion
/// wäre eine Kopie mit zwanzig Feldern, die bei jeder Schemaänderung mitgepflegt werden müsste.
/// Geladen wird mit <c>AsNoTracking</c>, die Objekte hängen also an keinem DbContext und
/// überleben dessen Entsorgung. Der Preis steht im Vertrag von
/// <see cref="ISurfaceRouteTable.LoadAsync"/>: Sie sind zum Lesen da. Wer ein Element verändert,
/// verändert es für jede weitere Anfrage, bis jemand schreibt.
/// </para>
/// <para>
/// <b>Zwei Stufen, wie bei Shopware.</b> Die Ablaufzeit ist nur das Netz für den Fall, dass eine
/// Invalidierung vergessen wurde oder eine zweite Instanz geschrieben hat — ein Prozess sieht die
/// Schreibvorgänge des anderen nicht. Sie ist bewusst kurz genug, dass ein solcher Fehler Minuten
/// und nicht Stunden dauert, und lang genug, dass sie im Normalbetrieb nie greift.
/// </para>
/// </remarks>
public sealed class CachedSurfaceRouteTable(
    IMemoryCache cache,
    IServiceScopeFactory scopeFactory) : ISurfaceRouteTable
{
    private const string CacheKey = "surface-route-table";

    /// <summary>
    /// Rückfallebene, kein Steuerungsmittel: Im Normalbetrieb wirft jeder Schreibvorgang den
    /// Eintrag weg, lange bevor diese Zeit abläuft.
    /// </summary>
    private static readonly TimeSpan Fallback = TimeSpan.FromMinutes(2);

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<WorkspaceSurface>> LoadAsync(CancellationToken cancellationToken = default) =>
        cache.TryGetValue(CacheKey, out IReadOnlyList<WorkspaceSurface>? cached) && cached is not null
            ? ValueTask.FromResult(cached)
            : new ValueTask<IReadOnlyList<WorkspaceSurface>>(LoadAndCacheAsync(cancellationToken));

    /// <inheritdoc />
    public void Invalidate() => cache.Remove(CacheKey);

    private async Task<IReadOnlyList<WorkspaceSurface>> LoadAndCacheAsync(CancellationToken cancellationToken)
    {
        // Eigener Scope: Der Dienst lebt für den Prozess, der DbContext für eine Anfrage. Ihn
        // festzuhalten hieße, eine Verbindung über die Anfrage hinaus zu binden — derselbe Fehler,
        // den SurfaceContextRevalidator einmal als 500 an beliebiger Stelle sichtbar gemacht hat.
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HostPersistenceDbContext>();

        var surfaces = await dbContext.WorkspaceSurfaces
            .AsNoTracking()
            .Include(x => x.Workspace)
            .ThenInclude(w => w.Tenant)
            // Feste Reihenfolge, damit bei Punktgleichstand nicht die Laune der Datenbank
            // entscheidet — dieselbe Zusage wie vorher in der Abfrage.
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        cache.Set(CacheKey, (IReadOnlyList<WorkspaceSurface>)surfaces, Fallback);
        return surfaces;
    }
}
