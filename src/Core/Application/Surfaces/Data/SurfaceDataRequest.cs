using Callora.Core.Application.Surfaces;

namespace Callora.Core.Application.Surfaces.Data;

/// <summary>
/// What a data contributor is asked about.
/// </summary>
/// <param name="WorkspaceKey">The workspace this surface belongs to.</param>
/// <param name="SurfaceKey">The surface being rendered.</param>
/// <param name="Path">
/// The path WITHIN the surface, with its public prefix already removed: a surface mounted at
/// <c>/shop</c> hands <c>/produkt/schuhe</c>, not <c>/shop/produkt/schuhe</c>. Stripping it here
/// rather than in every contributor is the point — the first one to get it wrong on a surface
/// mounted at <c>/</c> would never find out.
/// </param>
/// <param name="Locale">The surface's locale.</param>
/// <param name="Caller">
/// Who is looking, or null when nobody was established. Present so a contributor can shape its
/// answer — NOT so it can decide whether to answer at all; that follows from its declared
/// visibility and the host enforces it.
/// </param>
public sealed record SurfaceDataRequest(
    string WorkspaceKey,
    string SurfaceKey,
    string Path,
    string Locale,
    SurfaceCaller? Caller);
