using Callora.Core.Extensibility;

namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Handles one plugin-provided Admin API request.
/// </summary>
[CalloraExtensible("Extension point — implement to handle a plugin Admin API route (REV2 §8.2)")]
public interface IHostAdminApiRouteHandler
{
    /// <summary>
    /// Executes the route operation.
    /// </summary>
    ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default);
}
