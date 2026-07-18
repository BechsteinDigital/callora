using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins.Signing;

/// <summary>
/// Signature algorithm identifiers used in a plugin signature manifest. ECDSA
/// P-256 over SHA-256 is the only supported scheme (compact, cross-platform).
/// </summary>
[CalloraInternal("Plugin signature algorithm ids — not a plugin contract (REV2 §7.2)")]
public static class PluginSignatureAlgorithms
{
    public const string EcdsaP256Sha256 = "ECDSA-P256-SHA256";
}
