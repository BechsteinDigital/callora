namespace Callora.Core.Application.Surfaces.Data;

/// <summary>
/// What the contributors produced for one request, and what the host must do about it.
/// </summary>
/// <param name="Values">Namespace → the contributor's values. Ready for the template.</param>
/// <param name="Cacheable">
/// False as soon as one caller-specific contributor produced anything. The response must then
/// carry <c>no-store</c>: a proxy in front would otherwise hand the first visitor's data to
/// everyone after them — the quiet failure of this whole pattern, and the reason this is a
/// framework decision rather than a contributor's.
/// </param>
/// <param name="FailedRequiredNamespace">
/// Set when a contributor marked <c>Required</c> could not answer — it threw or ran out of time.
/// The page must not be rendered: a product page without its product looks complete and is false.
/// The host answers 503; the thing may well exist, we just could not reach it.
/// </param>
/// <param name="MissingRequiredNamespace">
/// Set when a contributor marked <c>Required</c> said the path names nothing. The host answers
/// 404 — a different answer from the one above, and only the contributor could tell them apart.
/// </param>
/// <param name="Skipped">
/// Optional contributors that failed or ran out of time, by namespace. Diagnostics — never their
/// values, and never why.
/// </param>
public sealed record SurfaceDataComposition(
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> Values,
    bool Cacheable,
    string? FailedRequiredNamespace,
    string? MissingRequiredNamespace,
    IReadOnlyList<string> Skipped)
{
    /// <summary>Nothing contributed: no data, cacheable, nothing failed.</summary>
    public static readonly SurfaceDataComposition Empty = new(
        new Dictionary<string, IReadOnlyDictionary<string, object?>>(StringComparer.Ordinal),
        Cacheable: true,
        FailedRequiredNamespace: null,
        MissingRequiredNamespace: null,
        Skipped: []);
}
