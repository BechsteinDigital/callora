namespace Callora.Core.Api;

public sealed record PluginLifecycleRequest(
    string? RequestedBy = null,
    string? WorkspaceKey = null);
