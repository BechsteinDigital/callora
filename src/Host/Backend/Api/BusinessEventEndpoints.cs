using Callora.Host.Backend.Application.Events.Business;
using Callora.Host.Backend.Infrastructure.Security;

namespace Callora.Host.Backend.Api;

/// <summary>
/// Discovery of the platform's business events (PLAT-270): the flow-builder
/// and webhook UI list the available event names and their fields here.
/// </summary>
public static class BusinessEventEndpoints
{
    public static void MapBusinessEventEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/events/catalog", (BusinessEventRegistry registry) =>
                Results.Ok(registry.ListDescriptors()))
            .WithName("BusinessEvents_Catalog")
            .WithTags("Events")
            .RequirePermission(BackendPermissionKeys.FlowRead);
    }
}
