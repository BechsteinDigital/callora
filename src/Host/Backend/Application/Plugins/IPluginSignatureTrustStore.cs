namespace Callora.Host.Backend.Application.Plugins;

public interface IPluginSignatureTrustStore
{
    bool IsTrusted(string? signerThumbprint);

    IReadOnlyList<TrustedPluginSigner> GetTrustedSigners();
}
