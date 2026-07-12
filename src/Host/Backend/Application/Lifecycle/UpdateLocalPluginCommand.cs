namespace Callora.Host.Backend.Application.Lifecycle;

public sealed record UpdateLocalPluginCommand(
    string PluginId,
    bool BuildIfNeeded = true,
    bool ForceBuild = false,
    string? RequestedBy = null);
