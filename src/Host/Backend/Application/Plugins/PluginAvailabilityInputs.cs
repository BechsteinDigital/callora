namespace Callora.Host.Backend.Application.Plugins;

/// <summary>
/// Observed truth of each availability factor for one plugin in one workspace.
/// The pure input to <see cref="PluginAvailability.From"/>.
/// </summary>
public readonly record struct PluginAvailabilityInputs(
    bool BundledOrInstalled,
    bool RuntimeHealthy,
    bool Entitled,
    bool WorkspaceEnabled,
    bool TenantActive,
    bool WorkspaceActive,
    bool RequiredCapabilitiesAvailable);
