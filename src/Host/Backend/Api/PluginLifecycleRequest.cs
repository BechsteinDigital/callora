namespace Callora.Host.Backend.Api;

public sealed record PluginLifecycleRequest(
    string? RequestedBy = null,
    string? WorkspaceKey = null);
