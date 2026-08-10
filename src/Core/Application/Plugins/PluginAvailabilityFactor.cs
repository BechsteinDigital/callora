namespace Callora.Core.Application.Plugins;

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

    /// <summary>
    /// Das Plugin hat sein Fehlerbudget nicht überschritten (<see cref="PluginFaultRegistry"/>).
    /// Trennt die wiederholt fehlschlagende Arbeit vom Lebenszyklus: Ein Plugin kann aktiv und
    /// <see cref="RuntimeHealthy"/> sein und trotzdem bei jeder Anfrage werfen.
    /// </summary>
    WithinFaultBudget,
}
