using System.Security.Cryptography;
using System.Text;

namespace Callora.Core.Application.Security;

/// <summary>
/// Mints single-use secrets and hashes them for storage. Used wherever the platform hands a caller a
/// bearer string it will present exactly once: surface handoff tickets, session resume promises.
/// </summary>
/// <remarks>
/// The hash is a plain SHA-256 rather than a password hash on purpose: the input already has full
/// entropy, so there is nothing to brute force and nothing a work factor would buy.
/// </remarks>
public static class SingleUseSecret
{
    private const int EntropyBytes = 32;

    /// <summary>Creates a fresh secret, URL-safe so it survives a query string unescaped.</summary>
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
