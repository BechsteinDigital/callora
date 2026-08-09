namespace Callora.Core.Application.Surfaces.Layout;

/// <summary>One placed block: which block, where in the section, and how its controls are bound.</summary>
/// <param name="BlockId">The block's id — also its view id and its island attribute.</param>
/// <param name="Region">Region of the section layout it sits in.</param>
/// <param name="Position">Order within the region, ascending.</param>
/// <param name="Config">
/// Control name → binding. Ein Block OHNE gebundene Steuerelemente hat eine leere Zuordnung,
/// keine fehlende.
/// </param>
/// <remarks>
/// Der Editor schreibt <c>config</c> nur, wenn etwas gebunden ist — ein frisch platzierter Block
/// steht als <c>{"blockId":"…","region":"main","position":0}</c> im Dokument. Beim
/// Deserialisieren kam <see cref="Config"/> damit als <see langword="null"/> zurück, und der
/// Kompositions-Renderer lief beim ERSTEN Block in eine NullReferenceException: 500 auf jeder
/// Seite, die jemand gebaut hatte, mit einer Trace-ID und ohne Hinweis auf das Layout.
///
/// <para>
/// Deshalb der Standardwert am Vertrag und nicht ein Null-Check im Serializer: Sonst müsste ihn
/// jede weitere Stelle wiederholen, die einen Block anfasst — und die erste, die es vergisst,
/// bringt wieder eine ganze Seite zu Fall.
/// </para>
/// </remarks>
public sealed record SurfaceLayoutBlock(
    string BlockId,
    string Region,
    int Position,
    IReadOnlyDictionary<string, SurfaceBlockBinding>? Config = null)
{
    /// <inheritdoc cref="Config"/>
    public IReadOnlyDictionary<string, SurfaceBlockBinding> Config { get; init; } =
        Config ?? new Dictionary<string, SurfaceBlockBinding>(StringComparer.Ordinal);
}
