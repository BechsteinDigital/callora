namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Issuers the host mints itself. The <see cref="ReservedPrefix"/> namespace is
/// refused from a plugin provider so a plugin can never impersonate the platform
/// (ADR-017 §3).
/// </summary>
public static class SurfaceIdentityIssuers
{
    /// <summary>Namespace no plugin provider may issue under.</summary>
    public const string ReservedPrefix = "callora.";

    /// <summary>
    /// A recognised but unauthenticated visitor. Carries no authority whatsoever —
    /// it exists so a plugin has a stable key for cart, draft or progress state.
    /// </summary>
    public const string Guest = "callora.surface-guest";

    /// <summary>
    /// Derived from an authenticated backend principal on a surface with no provider
    /// assigned. Deliberately carries no admin permissions as claims (ADR-017 §7).
    /// </summary>
    public const string Host = "callora.host";

    /// <summary>
    /// Whether an issuer belongs to the host's reserved namespace.
    /// </summary>
    /// <param name="issuer">Issuer to test.</param>
    public static bool IsReserved(string? issuer) =>
        issuer is not null &&
        issuer.StartsWith(ReservedPrefix, StringComparison.OrdinalIgnoreCase);
}
