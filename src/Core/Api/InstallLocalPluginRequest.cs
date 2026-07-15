namespace Callora.Core.Api;

public sealed record InstallLocalPluginRequest(
    string PluginId,
    bool BuildIfNeeded = true,
    bool ForceBuild = false,
    string? RequestedBy = null);
