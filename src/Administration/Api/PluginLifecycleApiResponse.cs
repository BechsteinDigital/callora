namespace Callora.Administration.Api;

public sealed record PluginLifecycleApiResponse(
    bool IsSuccess,
    string? PluginId,
    string? Message,
    string? ErrorCode = null,
    string? WarningMessage = null,
    string? WarningCode = null);
