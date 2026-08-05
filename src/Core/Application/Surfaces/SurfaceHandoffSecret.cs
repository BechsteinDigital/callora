using System.Security.Cryptography;
using System.Text;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Mints handoff secrets and hashes them for storage (ADR-017 §8.4). The hash is a
/// plain SHA-256 rather than a password hash on purpose: the input already has full
/// entropy, so there is nothing to brute force and nothing a work factor would buy.
/// </summary>
public static class SurfaceHandoffSecret
{
    private const int EntropyBytes = 32;

    /// <summary>Creates a fresh single-use secret.</summary>
    public static string Create() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(EntropyBytes))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    /// <summary>Hashes a secret for storage and lookup.</summary>
    /// <param name="secret">The secret to hash.</param>
    public static string Hash(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    }
}
