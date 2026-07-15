namespace Callora.Administration.Api;

public sealed record PluginLifecycleRequest(
    string? RequestedBy = null,
    string? WorkspaceKey = null);
