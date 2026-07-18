using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins;

[CalloraInternal("Plugin signer trust store — not a plugin contract (REV2 §7.2)")]
public interface IPluginSignatureTrustStore
{
    // Membership check for listing/diagnostics (Admin surfacing, B4). The verifier
    // itself uses ResolvePublicKeyPem — a fingerprint alone cannot verify a signature.
    bool IsTrusted(string? signerThumbprint);

    /// <summary>
    /// The trusted signer's public key (PEM) for the given key fingerprint, or null
    /// if the fingerprint is not a trusted signer with a resolvable key. The verifier
    /// needs the key to check an ECDSA manifest signature — a fingerprint alone
    /// (e.g. a legacy Authenticode thumbprint) resolves to null and fails closed.
    /// </summary>
    string? ResolvePublicKeyPem(string? signerFingerprint);

    IReadOnlyList<TrustedPluginSigner> GetTrustedSigners();
}
