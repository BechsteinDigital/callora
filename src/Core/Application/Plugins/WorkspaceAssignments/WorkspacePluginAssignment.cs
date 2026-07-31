namespace Callora.Core.Application.Plugins.WorkspaceAssignments;

public sealed class WorkspacePluginAssignment
{
    public WorkspacePluginAssignment(
        string pluginId,
        string displayName,
        bool isGloballyActive,
        bool isEntitled,
        bool isActive,
        bool isAssigned)
    {
        PluginId = pluginId;
        DisplayName = displayName;
        IsGloballyActive = isGloballyActive;
        IsEntitled = isEntitled;
        IsActive = isActive;
        IsAssigned = isAssigned;
    }

    public string PluginId { get; }

    public string DisplayName { get; }

    public bool IsGloballyActive { get; }

    public bool IsEntitled { get; }

    public bool IsActive { get; }

    public bool IsAssigned { get; }
}
