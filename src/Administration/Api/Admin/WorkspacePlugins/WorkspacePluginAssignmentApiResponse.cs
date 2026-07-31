namespace Callora.Administration.Api.Admin.WorkspacePlugins;

public sealed class WorkspacePluginAssignmentApiResponse
{
    public WorkspacePluginAssignmentApiResponse(
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
