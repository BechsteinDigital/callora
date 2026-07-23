namespace Callora.Core.Application.Plugins;

/// <summary>
/// The effective state of one tracked runtime capability inside <see cref="RuntimeCapabilityRegistry"/>,
/// keeping the original-cased identity so emitted <see cref="RuntimeCapabilityFlip"/>s are faithful.
/// </summary>
internal sealed record RuntimeCapabilityEntry(string PluginId, string Capability, string? WorkspaceKey, bool Satisfied);
