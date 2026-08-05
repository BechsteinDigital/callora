using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Handles one plugin-provided surface API request (#125 block B).
/// <para>
/// The host has already established who is calling and that the plugin may run in
/// that workspace. What remains is the question only the plugin can answer: may
/// <em>this</em> subject perform <em>this</em> action. The platform transports claims
/// and interprets none of them, so a handler that skips its own authorization has
/// none.
/// </para>
/// </summary>
[CalloraExtensible("Extension point — implement to handle a plugin surface API route (#125 block B)")]
public interface IHostSurfaceApiRouteHandler
{
    /// <summary>Executes the route operation.</summary>
    /// <param name="request">The surface request, carrying the established caller.</param>
    /// <param name="cancellationToken">Cancelled when the host's execution deadline elapses.</param>
    ValueTask<HostSurfaceApiResponse> HandleAsync(
        HostSurfaceApiRequest request,
        CancellationToken cancellationToken = default);
}
