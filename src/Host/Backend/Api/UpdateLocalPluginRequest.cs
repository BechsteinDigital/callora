namespace Callora.Host.Backend.Api;

public sealed record UpdateLocalPluginRequest(
    bool BuildIfNeeded = true,
    bool ForceBuild = false,
    string? RequestedBy = null);
