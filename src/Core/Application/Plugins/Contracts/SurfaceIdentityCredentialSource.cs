namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// One credential source an <see cref="IHostSurfaceIdentityProvider"/> declares it
/// needs. The declaration is part of the plugin contract and therefore review and
/// consent material: an operator can see which request values a provider is handed
/// before assigning it to a surface (ADR-017 §4).
/// </summary>
/// <param name="Kind">Whether the value is read from a header or a cookie.</param>
/// <param name="Name">
/// Name of the header or cookie. Matched case-insensitively; a source the request
/// does not carry is simply absent from the forwarded credentials.
/// </param>
public sealed record SurfaceIdentityCredentialSource(
    SurfaceIdentityCredentialKind Kind,
    string Name);
