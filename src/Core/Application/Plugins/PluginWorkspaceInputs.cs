namespace Callora.Core.Application.Plugins;

/// <summary>
/// Observed truth of the availability factors that only exist relative to one workspace:
/// the workspace activated the plugin, the workspace and its tenant are active, and the
/// capabilities the plugin requires are provided there.
/// </summary>
/// <remarks>
/// Paired with <see cref="PluginPlatformInputs"/> to form a workspace verdict. Kept
/// separate so a platform-wide question — "may this plugin do any work on this host at
/// all" — cannot accidentally answer with facts about a workspace nobody named.
/// </remarks>
public readonly record struct PluginWorkspaceInputs(
    bool WorkspaceEnabled,
    bool TenantActive,
    bool WorkspaceActive,
    bool RequiredCapabilitiesAvailable);
