using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins.Signing;

/// <summary>
/// A single covered file in a plugin signature manifest: its plugin-root-relative
/// path and the uppercase-hex SHA-256 of its contents.
/// </summary>
[CalloraInternal("Plugin signature manifest shape — not a plugin contract (REV2 §7.2)")]
public sealed record PluginSignatureFileHash(string Path, string Sha256);
