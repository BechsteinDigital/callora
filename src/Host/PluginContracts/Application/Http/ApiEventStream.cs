namespace Callora.Host.PluginContracts.Application.Http;

/// <summary>
/// Server-sent-events writer for streaming controller actions. The host
/// sets content type and flushing; the action only pushes event payloads.
/// </summary>
public abstract class ApiEventStream
{
    /// <summary>Fires when the consumer disconnects or the host shuts down.</summary>
    public abstract CancellationToken Aborted { get; }

    /// <summary>Serializes the payload as JSON and writes one SSE data frame.</summary>
    public abstract Task WriteEventAsync(object payload, CancellationToken cancellationToken = default);
}
