using System.Security.Cryptography;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Mints guest subjects (ADR-017 §3). The id is cryptographically random rather than
/// sequential or derived: a guessable guest subject would let one visitor address
/// another's cart or draft, even though the subject itself authorises nothing.
/// </summary>
public static class SurfaceGuestSubjectFactory
{
    private const int EntropyBytes = 16;

    /// <summary>Creates a fresh guest subject under the reserved guest issuer.</summary>
    public static SurfaceSubject Create() =>
        new(SurfaceIdentityIssuers.Guest, Base64UrlEncode(RandomNumberGenerator.GetBytes(EntropyBytes)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
