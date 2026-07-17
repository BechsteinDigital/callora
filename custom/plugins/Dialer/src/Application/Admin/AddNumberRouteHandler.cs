using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugins.Dialer.Application.Numbers;
using System.Text.Json;

namespace Callora.Plugins.Dialer.Application.Admin;

public sealed class AddNumberRouteHandler(IDialNumberStore store) : IHostAdminApiRouteHandler
{
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AdminRequestWorkspace.TryGet(request, out var workspaceKey, out var error))
        {
            return error!;
        }

        if (request.Body is not { ValueKind: JsonValueKind.Object } body ||
            !body.TryGetProperty("number", out var numberElement) ||
            numberElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(numberElement.GetString()))
        {
            return new HostAdminApiResponse(400, new { message = "Property 'number' (string) is required." });
        }

        string? displayName = null;
        if (body.TryGetProperty("displayName", out var displayNameElement) &&
            displayNameElement.ValueKind == JsonValueKind.String)
        {
            displayName = displayNameElement.GetString();
        }

        var entry = await store
            .AddAsync(workspaceKey, numberElement.GetString()!, displayName, cancellationToken)
            .ConfigureAwait(false);

        return new HostAdminApiResponse(201, entry);
    }
}
