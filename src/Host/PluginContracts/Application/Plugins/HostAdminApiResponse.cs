namespace Callora.Host.PluginContracts.Application.Plugins;

/// <summary>
/// Response model returned by plugin-provided Admin API handlers.
/// </summary>
/// <param name="StatusCode">HTTP status code.</param>
/// <param name="Payload">Optional JSON-serializable payload.</param>
public sealed record HostAdminApiResponse(
    int StatusCode,
    object? Payload = null);
