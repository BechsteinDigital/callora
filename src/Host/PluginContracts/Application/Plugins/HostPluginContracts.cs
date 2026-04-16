namespace VoipHost.PluginContracts.Application.Plugins;

/// <summary>
/// Host view of plugin state.
/// </summary>
public enum HostPluginState
{
    Installed = 0,
    Active = 1,
    Inactive = 2,
}

/// <summary>
/// Lifecycle operation kind.
/// </summary>
public enum HostPluginOperation
{
    Install = 0,
    Activate = 1,
    Deactivate = 2,
    Uninstall = 3,
}

/// <summary>
/// Host view for one plugin descriptor.
/// </summary>
public sealed record HostPluginDescriptor(
    string PluginId,
    string DisplayName,
    string AssemblyPath,
    string? EntryTypeName,
    HostPluginState State);

/// <summary>
/// Generic lifecycle operation result used by host tooling.
/// </summary>
public sealed record HostPluginOperationResult(
    HostPluginOperation Operation,
    bool IsSuccess,
    string? PluginId = null,
    string? Message = null);
