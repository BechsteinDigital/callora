using System.Security.Cryptography;
using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins.Signing;

/// <summary>
/// ECDSA P-256 signing/verification and signer fingerprinting for plugin signature
/// manifests. Cross-platform (unlike the deprecated Authenticode path). The signer
/// fingerprint is the SHA-256 of the public key's SubjectPublicKeyInfo — the trust
/// unit stored in the trust store.
/// </summary>
[CalloraInternal("Plugin signature cryptography — not a plugin contract (REV2 §7.2)")]
public static class PluginSignatureCryptography
{
    public static string ComputeFingerprint(ECDsa key)
    {
        var subjectPublicKeyInfo = key.ExportSubjectPublicKeyInfo();
        return Convert.ToHexString(SHA256.HashData(subjectPublicKeyInfo));
    }

    public static string Sign(ReadOnlySpan<byte> canonicalBytes, ECDsa privateKey) =>
        Convert.ToBase64String(privateKey.SignData(canonicalBytes, HashAlgorithmName.SHA256));

    public static bool Verify(ReadOnlySpan<byte> canonicalBytes, string signatureBase64, ECDsa publicKey)
    {
        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(signatureBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        return publicKey.VerifyData(canonicalBytes, signature, HashAlgorithmName.SHA256);
    }
}
