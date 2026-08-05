namespace Callora.Administration.Api;

/// <summary>
/// One plugin that may be assigned as a surface's identity provider — filtered on the
/// <c>surface.identity</c> capability (ADR-017 §5.1).
/// </summary>
/// <param name="PluginId">Stable plugin identifier.</param>
/// <param name="DisplayName">Human-readable plugin name.</param>
/// <param name="Version">Installed version, when the package registry reports one.</param>
/// <param name="IsAvailable">Whether the plugin is effectively available in the workspace.</param>
public sealed record SurfaceIdentityProviderCandidateApiResponse(
    string PluginId,
    string DisplayName,
    string? Version,
    bool IsAvailable);
