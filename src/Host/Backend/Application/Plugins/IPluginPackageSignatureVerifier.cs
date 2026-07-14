namespace Callora.Host.Backend.Application.Plugins;

public interface IPluginPackageSignatureVerifier
{
    ValueTask<PluginPackageSignatureVerificationResult> VerifyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default);
}
