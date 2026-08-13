using Callora.Core.Application.Extensions;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Ein Theme-Cache, der nichts hält und nur zählt.
/// </summary>
/// <remarks>
/// Dasselbe Muster wie <see cref="PassThroughSurfaceRouteTable"/>: Tests, die Persistenz prüfen,
/// sollen sehen, was sie gerade geschrieben haben — und Tests, die die Invalidierung prüfen,
/// brauchen einen Zähler statt eines echten Caches. Ob ein Schreibvorgang das gecachte Theme
/// verwirft, ist keine Nebensache: Ein vergessener Aufruf zeigt sich nicht als Fehler, sondern
/// als Betreiber, der eine Farbe ändert und sie nicht wiederfindet.
/// </remarks>
public sealed class CountingThemeResolutionCache : IThemeResolutionCache
{
    private int _invalidationCount;

    /// <summary>Wie oft <see cref="Invalidate"/> gerufen wurde.</summary>
    public int InvalidationCount => Volatile.Read(ref _invalidationCount);

    /// <inheritdoc />
    public void Invalidate() => Interlocked.Increment(ref _invalidationCount);
}
