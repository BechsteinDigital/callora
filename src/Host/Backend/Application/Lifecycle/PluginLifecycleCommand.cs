namespace Callora.Host.Backend.Application.Lifecycle;

public sealed record PluginLifecycleCommand(
    string PluginId,
    string? RequestedBy = null,
    string? WorkspaceKey = null);
