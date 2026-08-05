using Callora.Core.Application.Plugins;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Security;

namespace Callora.Administration.Api;

/// <summary>
/// The contract catalog of one installation (#125 block D).
/// <para>
/// Type identity across load contexts has been solved for a while; what was missing was the ability
/// to see it. Without this endpoint an operator cannot tell which contracts an installation offers,
/// which plugins are bound to them, or what a contract update would break before applying it, and
/// plugin combinability stays insider knowledge.
/// </para>
/// </summary>
public static class ContractCatalogEndpoints
{
    public static IEndpointRouteBuilder MapContractCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/api/plugins/contracts", async (
                ContractCatalogService catalog,
                CancellationToken cancellationToken) =>
            {
                var entries = await catalog.ListAsync(cancellationToken).ConfigureAwait(false);
                return Results.Ok(entries.Select(ToResponse).ToArray());
            })
            .WithTags("Plugin Extensions")
            .WithName("PluginExtensions_ContractCatalog")
            .RequireAuthorization()
            .RequirePermission(BackendPermissionKeys.ExtensionRead);

        return endpoints;
    }

    private static ContractCatalogApiResponse ToResponse(ContractCatalogEntry entry) =>
        new(
            entry.AssemblyName,
            entry.Version,
            entry.DeclaringPluginId,
            entry.IsHostProvided,
            entry.RequiresRestartToChange,
            entry.Dependents
                .Select(static dependent => new ContractDependentApiResponse(
                    dependent.PluginId, dependent.RequiredRange, dependent.IsSatisfied))
                .ToArray());
}
