namespace Callora.Core.Application.Policies;

public sealed class BackendTrustedSignerOptions
{
    public string PublisherId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Legacy signer fingerprint/thumbprint (listed only). A signer without a
    /// <see cref="PublicKey"/> cannot verify an ECDSA manifest signature.
    /// </summary>
    public string Thumbprint { get; set; } = string.Empty;

    /// <summary>
    /// The signer's public key in PEM (SubjectPublicKeyInfo, "BEGIN PUBLIC KEY").
    /// Required to verify a plugin signature; the key fingerprint (SHA-256 of the
    /// SPKI) is derived from it and used as the trust unit.
    /// </summary>
    public string PublicKey { get; set; } = string.Empty;

    public string Source { get; set; } = "config";
}
