namespace Callora.Host.Backend.Application.Plugins;

/// <summary>
/// One precondition of a plugin being effectively available in a workspace
/// (REV2 §3.2). A plugin is available only when every factor holds.
/// </summary>
public enum PluginAvailabilityFactor
{
    BundledOrInstalled,
    RuntimeHealthy,
    Entitled,
    WorkspaceEnabled,
    TenantActive,
    WorkspaceActive,
    RequiredCapabilitiesAvailable,
}
