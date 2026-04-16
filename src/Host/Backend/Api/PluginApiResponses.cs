namespace Callora.Host.Backend.Api;

public sealed record PluginLifecycleApiResponse(
    bool IsSuccess,
    string? PluginId,
    string? Message);
