namespace Callora.Core.Application.Surfaces;

/// <summary>
/// A plugin an operator may assign as a surface's identity provider — one that
/// declares the <c>surface.identity</c> capability (ADR-017 §5.1). Filtering on the
/// capability is what keeps the assignment dropdown from offering every installed
/// plugin, most of which could never answer the question.
/// </summary>
/// <param name="PluginId">Stable plugin identifier.</param>
/// <param name="DisplayName">Human-readable plugin name.</param>
/// <param name="Version">Installed version, when the package registry reports one.</param>
/// <param name="IsAvailable">Whether the plugin is effectively available in the workspace.</param>
public sealed record SurfaceIdentityProviderCandidate(
    string PluginId,
    string DisplayName,
    string? Version,
    bool IsAvailable);
