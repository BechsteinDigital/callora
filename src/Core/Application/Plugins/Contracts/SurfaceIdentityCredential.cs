namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// One resolved credential value handed to an <see cref="IHostSurfaceIdentityProvider"/>.
/// The host only ever materialises credentials for the sources the provider declared
/// (ADR-017 §4); everything else about the request stays behind the seam.
/// </summary>
/// <param name="Kind">Whether the value came from a header or a cookie.</param>
/// <param name="Name">Name of the declared source the value was read from.</param>
/// <param name="Value">The raw value as sent by the client — unvalidated by the host.</param>
public sealed record SurfaceIdentityCredential(
    SurfaceIdentityCredentialKind Kind,
    string Name,
    string Value);
