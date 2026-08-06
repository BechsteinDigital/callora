namespace Callora.Core.Application.Surfaces;

/// <summary>
/// One context value on its way to a browser. The wire shape of the bridge — deliberately
/// small, because the runtime does nothing with it but hand the value to the local channel
/// under <see cref="Key"/>.
/// </summary>
/// <param name="Key">Namespaced, versioned key, e.g. <c>communication.active-call/v1</c>.</param>
/// <param name="Value">The value as JSON, or null to clear the key.</param>
public sealed record SurfaceContextMessage(string Key, object? Value);
