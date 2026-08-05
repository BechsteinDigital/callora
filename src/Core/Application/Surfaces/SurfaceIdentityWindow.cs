namespace Callora.Core.Application.Surfaces;

/// <summary>
/// The validity window of an identity after the host clamped it: the authentication
/// time never lies in the future, and the expiry never exceeds
/// <see cref="SurfaceIdentityOptions.MaxIdentityLifetime"/> (ADR-017 §4).
/// </summary>
/// <param name="AuthenticatedAtUtc">When authentication happened.</param>
/// <param name="ExpiresAtUtc">Effective expiry after clamping.</param>
internal sealed record SurfaceIdentityWindow(
    DateTimeOffset AuthenticatedAtUtc,
    DateTimeOffset ExpiresAtUtc);
