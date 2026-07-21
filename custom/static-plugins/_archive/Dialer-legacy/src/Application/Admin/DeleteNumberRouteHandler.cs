using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugins.Dialer.Application.Numbers;

namespace Callora.Plugins.Dialer.Application.Admin;

public sealed class DeleteNumberRouteHandler(IDialNumberStore store) : IHostAdminApiRouteHandler
{
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AdminRequestWorkspace.TryGet(request, out var workspaceKey, out var error))
        {
            return error!;
        }

        if (!request.RouteValues.TryGetValue("numberId", out var numberId) || string.IsNullOrWhiteSpace(numberId))
        {
            return new HostAdminApiResponse(400, new { message = "Route value 'numberId' is required." });
        }

        var removed = await store.RemoveAsync(workspaceKey, numberId, cancellationToken).ConfigureAwait(false);
        if (!removed)
        {
            return new HostAdminApiResponse(404, new { message = $"Number '{numberId}' was not found." });
        }

        return new HostAdminApiResponse(204);
    }
}
