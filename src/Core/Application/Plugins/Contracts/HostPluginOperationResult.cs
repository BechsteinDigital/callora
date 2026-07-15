namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Generic lifecycle operation result used by host tooling.
/// </summary>
public sealed record HostPluginOperationResult(
    HostPluginOperation Operation,
    bool IsSuccess,
    string? PluginId = null,
    string? Message = null);
