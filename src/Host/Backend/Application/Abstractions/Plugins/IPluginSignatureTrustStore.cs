namespace Callora.Host.Backend.Application.Abstractions.Plugins;

public interface IPluginSignatureTrustStore
{
    bool IsTrusted(string? signerThumbprint);

    IReadOnlyList<TrustedPluginSigner> GetTrustedSigners();
}
