namespace Callora.Core.Application.Surfaces.Data;

/// <summary>
/// What a contributor has to say about a request. Three outcomes, not two — and the difference
/// between them is one only the contributor can draw.
/// <para>
/// „Dieses Produkt gibt es nicht" und „ich konnte den Katalog nicht erreichen" sind verschiedene
/// Antworten: 404 gegen 503. Ein erforderlicher Beitrag, der nur „hat nicht geklappt" melden
/// kann, zwingt den Host zu einer Wahl, die er nicht treffen kann — und beide Ausgänge sind
/// falsch. 404 für einen Ausfall heißt, der Kunde denkt, das Produkt sei weg. 503 für einen
/// Tippfehler in der URL heißt, Suchmaschinen behalten die Seite im Index.
/// </para>
/// </summary>
public sealed record SurfaceDataResult
{
    private SurfaceDataResult(
        IReadOnlyDictionary<string, object?>? values,
        bool notFound)
    {
        Values = values;
        NotFound = notFound;
    }

    /// <summary>The contributed values, or null when there are none.</summary>
    public IReadOnlyDictionary<string, object?>? Values { get; }

    /// <summary>
    /// The thing this path names does not exist. For a required contributor the host answers 404;
    /// for an optional one it is the same as having nothing to say.
    /// </summary>
    public bool NotFound { get; }

    /// <summary>Values for the template.</summary>
    public static SurfaceDataResult Contributed(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new SurfaceDataResult(values, notFound: false);
    }

    /// <summary>
    /// Nothing to contribute here, and that is fine — a catalog contributor on <c>/kontakt</c>.
    /// Not an error and not a missing page.
    /// </summary>
    public static readonly SurfaceDataResult Nothing = new(null, notFound: false);

    /// <summary>
    /// This path names something that does not exist. Only the contributor knows: the host sees a
    /// string, the contributor knows whether a product with that slug was ever there.
    /// </summary>
    public static readonly SurfaceDataResult Missing = new(null, notFound: true);
}
