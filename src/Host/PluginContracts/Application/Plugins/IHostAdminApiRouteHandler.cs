namespace VoipHost.PluginContracts.Application.Plugins;

/// <summary>
/// Handles one plugin-provided Admin API request.
/// </summary>
public interface IHostAdminApiRouteHandler
{
    /// <summary>
    /// Executes the route operation.
    /// </summary>
    ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default);
}
