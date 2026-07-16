using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins;

[CalloraInternal("Plugin signer trust store — not a plugin contract (REV2 §7.2)")]
public interface IPluginSignatureTrustStore
{
    bool IsTrusted(string? signerThumbprint);

    IReadOnlyList<TrustedPluginSigner> GetTrustedSigners();
}
