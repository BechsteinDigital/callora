namespace Callora.Host.Backend.Application.Abstractions.Plugins;

public interface IPluginPackageSignatureVerifier
{
    ValueTask<PluginPackageSignatureVerificationResult> VerifyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default);
}
