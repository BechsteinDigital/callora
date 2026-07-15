namespace Callora.Core.Api;

public sealed record UpdateLocalPluginRequest(
    bool BuildIfNeeded = true,
    bool ForceBuild = false,
    string? RequestedBy = null);
