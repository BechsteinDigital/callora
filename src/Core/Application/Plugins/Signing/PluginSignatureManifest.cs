using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins.Signing;

/// <summary>
/// The contents of a plugin's <c>plugin.signature.json</c>: the covered files with
/// their hashes, the signer's public-key fingerprint, and a detached signature over
/// the canonical serialization of everything except the signature itself. Signing
/// the manifest (which lists registry.json among the files) makes plugin metadata —
/// capabilities, entry type — tamper-evident, not just the assembly.
/// </summary>
[CalloraInternal("Plugin signature manifest shape — not a plugin contract (REV2 §7.2)")]
public sealed record PluginSignatureManifest(
    string SchemaVersion,
    string PluginId,
    string Version,
    string Algorithm,
    string SignerFingerprint,
    IReadOnlyList<PluginSignatureFileHash> Files,
    string? Signature);
