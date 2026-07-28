using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Handles one plugin-provided public HTTP request.
/// </summary>
/// <remarks>
/// Implementations run on the anonymous public surface (<c>/public/{pluginId}/…</c>)
/// and must perform all necessary input validation and access control themselves.
/// The host catches any unhandled exception and returns HTTP 500 without leaking
/// exception details to the caller.
/// </remarks>
[CalloraExtensible("Extension point — implement to handle a plugin public HTTP route (anonymous surface)")]
public interface IHostPublicHttpRouteHandler
{
    /// <summary>
    /// Executes the route operation and returns a response the host will write
    /// to the HTTP connection.
    /// </summary>
    /// <param name="request">The incoming request including route values, query, headers, and body.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be cancelled.</param>
    /// <returns>
    /// A <see cref="HostPublicHttpResponse"/> describing the status code,
    /// content type, body, and any additional response headers (e.g. a
    /// <c>Location</c> header for redirects).
    /// </returns>
    ValueTask<HostPublicHttpResponse> HandleAsync(
        HostPublicHttpRequest request,
        CancellationToken cancellationToken = default);
}
