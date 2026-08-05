using Callora.Core.Application.Security;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Mints handoff secrets and hashes them for storage (ADR-017 §8.4).
/// </summary>
/// <remarks>
/// The mechanics moved to <see cref="SingleUseSecret"/> once resume promises needed the same thing
/// (ADR-018 §2.2). This stays as the surface-facing name so a reader following the handoff flow does
/// not have to know that.
/// </remarks>
public static class SurfaceHandoffSecret
{
    /// <summary>Creates a fresh single-use secret.</summary>
    public static string Create() => SingleUseSecret.Create();

    /// <summary>Hashes a secret for storage and lookup.</summary>
    /// <param name="secret">The secret to hash.</param>
    public static string Hash(string secret) => SingleUseSecret.Hash(secret);
}
