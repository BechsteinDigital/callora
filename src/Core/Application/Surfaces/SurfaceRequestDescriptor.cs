namespace Callora.Core.Application.Surfaces;

/// <summary>
/// The transport-neutral facts about one surface request that an identity provider is
/// allowed to see (ADR-017 §4). Everything else about the request stays behind the seam.
/// </summary>
/// <param name="HttpMethod">HTTP method of the request.</param>
/// <param name="RoutePath">Request path relative to the surface's public path prefix.</param>
/// <param name="Locale">Effective surface locale.</param>
/// <param name="Origin">The request's <c>Origin</c> header when present.</param>
/// <param name="UserAgent">The request's <c>User-Agent</c> header when present.</param>
public sealed record SurfaceRequestDescriptor(
    string HttpMethod,
    string RoutePath,
    string Locale,
    string? Origin = null,
    string? UserAgent = null);
