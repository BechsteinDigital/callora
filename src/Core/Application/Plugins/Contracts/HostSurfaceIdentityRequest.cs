namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Everything an <see cref="IHostSurfaceIdentityProvider"/> is given to authenticate
/// a surface visitor (ADR-017 §4). Deliberately not an <c>HttpContext</c>: a provider
/// receives normalised request metadata plus the values of the credential sources it
/// declared, and nothing else — no raw headers, no cookie collection, no access to
/// the host's own session.
/// </summary>
/// <param name="TenantKey">Tenant owning the surface's workspace.</param>
/// <param name="WorkspaceKey">Workspace the surface belongs to.</param>
/// <param name="SurfaceKey">Surface the request was resolved to.</param>
/// <param name="HttpMethod">HTTP method of the originating request.</param>
/// <param name="RoutePath">Request path relative to the surface's public path prefix.</param>
/// <param name="Locale">Effective surface locale.</param>
/// <param name="Credentials">
/// Values for the provider's declared <see cref="IHostSurfaceIdentityProvider.CredentialSources"/>.
/// A declared source the request does not carry is absent rather than empty.
/// </param>
/// <param name="Origin">The request's <c>Origin</c> header when present.</param>
/// <param name="UserAgent">The request's <c>User-Agent</c> header when present.</param>
public sealed record HostSurfaceIdentityRequest(
    string TenantKey,
    string WorkspaceKey,
    string SurfaceKey,
    string HttpMethod,
    string RoutePath,
    string Locale,
    IReadOnlyList<SurfaceIdentityCredential> Credentials,
    string? Origin = null,
    string? UserAgent = null)
{
    /// <summary>
    /// Reads one declared credential, or <see langword="null"/> when the request did
    /// not carry it. Name comparison is case-insensitive, matching how headers and
    /// cookies are addressed elsewhere.
    /// </summary>
    /// <param name="kind">Source kind to look up.</param>
    /// <param name="name">Header or cookie name to look up.</param>
    public string? Credential(SurfaceIdentityCredentialKind kind, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        foreach (var credential in Credentials)
        {
            if (credential.Kind == kind &&
                string.Equals(credential.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return credential.Value;
            }
        }

        return null;
    }
}
