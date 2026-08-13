using Callora.Core.Application.Workspaces;
using Callora.Core.Domain.Workspaces;
using Callora.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Eine Flächentabelle ohne Gedächtnis: Sie lädt bei jedem Aufruf frisch und zählt mit, wie oft
/// sie für ungültig erklärt wurde.
/// </summary>
/// <remarks>
/// <para>
/// Für Tests, die Persistenz prüfen und nicht den Cache. Sie sollen sehen, was sie gerade
/// geschrieben haben — mit einem echten Cache davor prüften sie sonst beides gleichzeitig und
/// wären bei einem Fehlschlag zweideutig.
/// </para>
/// <para>
/// <see cref="InvalidationCount"/> macht die Gegenprobe möglich: Ob ein Schreibvorgang die
/// Tabelle verwirft, ist keine Nebensache, sondern die Bedingung dafür, dass der Cache in
/// Produktion zulässig ist — eine abgeschaltete Elternfläche nimmt ihre Kinder mit vom Netz, und
/// ein Eintrag, der das überlebt, liefert abgeschaltete Seiten weiter aus.
/// </para>
/// </remarks>
public sealed class PassThroughSurfaceRouteTable(HostPersistenceDbContext dbContext) : ISurfaceRouteTable
{
    private int _invalidationCount;

    /// <summary>Wie oft <see cref="Invalidate"/> gerufen wurde.</summary>
    public int InvalidationCount => Volatile.Read(ref _invalidationCount);

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<WorkspaceSurface>> LoadAsync(CancellationToken cancellationToken = default) =>
        await dbContext.WorkspaceSurfaces
            .AsNoTracking()
            .Include(x => x.Workspace)
            .ThenInclude(w => w.Tenant)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public void Invalidate() => Interlocked.Increment(ref _invalidationCount);
}
