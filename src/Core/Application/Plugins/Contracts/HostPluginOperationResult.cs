namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Generic lifecycle operation result used by host tooling.
/// </summary>
/// <param name="Operation">The lifecycle operation this result is for.</param>
/// <param name="IsSuccess">Whether the operation succeeded.</param>
/// <param name="PluginId">Plugin the operation targeted, if known.</param>
/// <param name="Message">Optional detail, typically the failure reason.</param>
public sealed record HostPluginOperationResult(
    HostPluginOperation Operation,
    bool IsSuccess,
    string? PluginId = null,
    string? Message = null);
