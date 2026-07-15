using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugins.Dialer.Application.Numbers;

namespace Callora.Plugins.Dialer.Application.Admin;

public sealed class ListNumbersRouteHandler(IDialNumberStore store) : IHostAdminApiRouteHandler
{
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AdminRequestWorkspace.TryGet(request, out var workspaceKey, out var error))
            return error!;

        var numbers = await store.ListAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        return new HostAdminApiResponse(200, numbers);
    }
}
