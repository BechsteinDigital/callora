using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins;

[CalloraInternal("Plugin package signature verification — not a plugin contract (REV2 §7.2)")]
public interface IPluginPackageSignatureVerifier
{
    ValueTask<PluginPackageSignatureVerificationResult> VerifyAsync(
        string assemblyPath,
        CancellationToken cancellationToken = default);
}
