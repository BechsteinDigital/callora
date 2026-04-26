using Callora.Host.Backend.Application.Abstractions.Plugins;

namespace Callora.Host.Backend.Tests.Support;

internal sealed class StaticPluginPackageSignatureVerifier : IPluginPackageSignatureVerifier
{
    public PluginPackageSignatureVerificationResult Result { get; set; } =
        new(IsValid: true);

    public ValueTask<PluginPackageSignatureVerificationResult> VerifyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(Result);
}
