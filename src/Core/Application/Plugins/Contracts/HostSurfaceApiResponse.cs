namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Response returned by a plugin-provided surface API handler (#125 block B).
/// </summary>
/// <param name="StatusCode">HTTP status code.</param>
/// <param name="Payload">Optional JSON-serializable payload.</param>
public sealed record HostSurfaceApiResponse(
    int StatusCode,
    object? Payload = null);
