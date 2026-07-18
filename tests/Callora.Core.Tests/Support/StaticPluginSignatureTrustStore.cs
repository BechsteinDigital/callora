using Callora.Core.Application.Plugins;

namespace Callora.Core.Tests.Support;

internal sealed class StaticPluginSignatureTrustStore : IPluginSignatureTrustStore
{
    public IReadOnlyList<TrustedPluginSigner> Signers { get; init; } = [];

    public IReadOnlyDictionary<string, string> PublicKeyPemByFingerprint { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public bool IsTrusted(string? signerThumbprint)
    {
        if (string.IsNullOrWhiteSpace(signerThumbprint))
        {
            return false;
        }

        return Signers.Any(x => string.Equals(x.Thumbprint, signerThumbprint, StringComparison.OrdinalIgnoreCase));
    }

    public string? ResolvePublicKeyPem(string? signerFingerprint) =>
        !string.IsNullOrWhiteSpace(signerFingerprint) && PublicKeyPemByFingerprint.TryGetValue(signerFingerprint, out var pem)
            ? pem
            : null;

    public IReadOnlyList<TrustedPluginSigner> GetTrustedSigners() => Signers;
}
