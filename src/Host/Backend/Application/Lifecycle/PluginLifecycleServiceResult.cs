namespace Callora.Host.Backend.Application.Lifecycle;

public sealed record PluginLifecycleServiceResult(
    PluginLifecycleServiceStatus Status,
    bool IsSuccess,
    string? PluginId = null,
    string? Message = null,
    string? ErrorCode = null,
    string? WarningMessage = null,
    string? WarningCode = null);
