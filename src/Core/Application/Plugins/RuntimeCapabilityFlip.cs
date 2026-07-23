namespace Callora.Core.Application.Plugins;

/// <summary>
/// Signals that a plugin's <em>effective</em> runtime-capability state changed (after any grace period)
/// for one capability in one scope. Consumers react by re-evaluating dependent availability and
/// invalidating availability-derived gates.
/// </summary>
/// <param name="PluginId">The plugin whose runtime capability flipped.</param>
/// <param name="Capability">The capability code.</param>
/// <param name="WorkspaceKey">The scope, or <see langword="null"/> for a global capability.</param>
/// <param name="Satisfied"><see langword="true"/> when the capability is now effectively provided; otherwise <see langword="false"/>.</param>
public sealed record RuntimeCapabilityFlip(string PluginId, string Capability, string? WorkspaceKey, bool Satisfied);
