using System.Security.Cryptography;
using System.Text;

namespace Callora.Core.Application.Integrations;

/// <summary>
/// Generates and hashes machine-to-machine integration keys (PLAT-264). Keys are
/// high-entropy random tokens, so a plain SHA-256 hash is a safe, collision-free
/// lookup index — no per-key salt is needed and enumeration is impossible. The
/// plaintext key is shown once at creation and never persisted.
/// </summary>
public static class IntegrationApiKey
{
    /// <summary>Recognisable prefix so leaked keys can be traced to Callora.</summary>
    public const string Prefix = "clra_";

    private const int PrefixKeepLength = 12;

    /// <summary>Creates a new random plaintext key.</summary>
    public static string Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Prefix + Base64Url(bytes);
    }

    /// <summary>Deterministic lookup hash of a key (Base64 of SHA-256).</summary>
    public static string ComputeHash(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key.Trim()));
        return Convert.ToBase64String(hash);
    }

    /// <summary>Leading characters kept for recognition; never enough to reconstruct the key.</summary>
    public static string DerivePrefix(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var trimmed = key.Trim();
        return trimmed.Length <= PrefixKeepLength ? trimmed : trimmed[..PrefixKeepLength];
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
