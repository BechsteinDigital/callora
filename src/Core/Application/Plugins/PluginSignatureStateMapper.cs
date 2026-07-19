using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Maps a signature verification result to a stable admin-facing state. Pure, so it
/// can be reused and tested independently of the verifier and the endpoint.
/// </summary>
[CalloraInternal("Plugin signature state mapping — not a plugin contract (REV2 §7.2)")]
public static class PluginSignatureStateMapper
{
    public static string Map(PluginPackageSignatureVerificationResult result)
    {
        if (result.IsValid)
        {
            // A valid-but-unsignerless result is an unsigned plugin allowed through
            // by AllowUnsignedPlugins — still "unsigned" from a trust standpoint.
            return string.IsNullOrWhiteSpace(result.SignerThumbprint)
                ? PluginSignatureStates.NotSigned
                : PluginSignatureStates.SignedTrusted;
        }

        return result.ErrorCode switch
        {
            PluginPackageSignatureErrorCodes.UnsignedPackage => PluginSignatureStates.NotSigned,
            PluginPackageSignatureErrorCodes.UntrustedSigner => PluginSignatureStates.Untrusted,
            PluginPackageSignatureErrorCodes.Revoked => PluginSignatureStates.Revoked,
            PluginPackageSignatureErrorCodes.ContentHashMismatch => PluginSignatureStates.ContentHashMismatch,
            _ => PluginSignatureStates.Invalid,
        };
    }
}
