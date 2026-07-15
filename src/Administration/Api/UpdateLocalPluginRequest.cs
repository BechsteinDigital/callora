namespace Callora.Administration.Api;

public sealed record UpdateLocalPluginRequest(
    bool BuildIfNeeded = true,
    bool ForceBuild = false,
    string? RequestedBy = null);
