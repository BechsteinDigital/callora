using Callora.Host.Backend.Application.Plugins;

namespace Callora.Host.Backend.Tests.Support;

internal sealed class StaticPluginSignatureTrustStore : IPluginSignatureTrustStore
{
    public IReadOnlyList<TrustedPluginSigner> Signers { get; init; } = [];

    public bool IsTrusted(string? signerThumbprint)
    {
        if (string.IsNullOrWhiteSpace(signerThumbprint))
        {
            return false;
        }

        return Signers.Any(x => string.Equals(x.Thumbprint, signerThumbprint, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<TrustedPluginSigner> GetTrustedSigners() => Signers;
}
