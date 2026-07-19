using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Stable API states describing a plugin's current signature standing, surfaced in
/// the admin UI. Derived from a re-verification of the installed assembly.
/// </summary>
[CalloraInternal("Plugin signature state codes — not a plugin contract (REV2 §7.2)")]
public static class PluginSignatureStates
{
    public const string SignedTrusted = "signed-trusted";
    public const string NotSigned = "unsigned";
    public const string Untrusted = "untrusted";
    public const string Revoked = "revoked";
    public const string ContentHashMismatch = "content-hash-mismatch";
    public const string Invalid = "invalid";
}
