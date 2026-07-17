using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugins.Dialer.Application.Runs;
using System.Text.Json;

namespace Callora.Plugins.Dialer.Application.Admin;

public sealed class StartDialRunRouteHandler(DialRunCoordinator coordinator) : IHostAdminApiRouteHandler
{
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AdminRequestWorkspace.TryGet(request, out var workspaceKey, out var error))
        {
            return error!;
        }

        var options = ParseOptions(request.Body);
        var snapshot = await coordinator.StartRunAsync(workspaceKey, options, cancellationToken).ConfigureAwait(false);
        if (snapshot is null)
        {
            return new HostAdminApiResponse(409, new { message = "A dial run is already in progress for this workspace." });
        }

        return new HostAdminApiResponse(202, snapshot);
    }

    private static DialRunOptions ParseOptions(JsonElement? body)
    {
        if (body is { ValueKind: JsonValueKind.Object } json &&
            json.TryGetProperty("callTimeoutSeconds", out var timeoutElement) &&
            timeoutElement.ValueKind == JsonValueKind.Number &&
            timeoutElement.TryGetInt32(out var timeoutSeconds) &&
            timeoutSeconds > 0)
        {
            return new DialRunOptions(TimeSpan.FromSeconds(timeoutSeconds));
        }

        return DialRunOptions.Default;
    }
}
