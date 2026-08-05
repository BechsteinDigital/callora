using Callora.Core.Application.Plugins.Contracts;

namespace Callora.Core.Tests.Support;

/// <summary>
/// A surface API handler with scripted behaviour: answers with a fixed status and
/// payload, throws, or stalls past the host's deadline.
/// </summary>
public sealed class StaticSurfaceApiRouteHandler : IHostSurfaceApiRouteHandler
{
    private readonly Func<HostSurfaceApiRequest, CancellationToken, Task<HostSurfaceApiResponse>> _handler;

    /// <summary>Answers with a status and an optional payload.</summary>
    /// <param name="statusCode">Status to answer with.</param>
    /// <param name="payload">Payload to answer with.</param>
    public StaticSurfaceApiRouteHandler(int statusCode, object? payload = null)
        : this((_, _) => Task.FromResult(new HostSurfaceApiResponse(statusCode, payload)))
    {
    }

    /// <summary>Answers with scripted behaviour.</summary>
    /// <param name="handler">What the handler does when invoked.</param>
    public StaticSurfaceApiRouteHandler(
        Func<HostSurfaceApiRequest, CancellationToken, Task<HostSurfaceApiResponse>> handler) =>
        _handler = handler;

    /// <summary>The request the handler last saw.</summary>
    public HostSurfaceApiRequest? LastRequest { get; private set; }

    public async ValueTask<HostSurfaceApiResponse> HandleAsync(
        HostSurfaceApiRequest request,
        CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return await _handler(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A handler that always throws.</summary>
    public static StaticSurfaceApiRouteHandler Throwing() =>
        new((_, _) => throw new InvalidOperationException("handler exploded"));

    /// <summary>A handler that never answers before the deadline elapses.</summary>
    public static StaticSurfaceApiRouteHandler Stalling() =>
        new(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
            return new HostSurfaceApiResponse(200);
        });
}
