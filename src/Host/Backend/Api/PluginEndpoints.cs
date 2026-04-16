using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Application.Lifecycle;
using Microsoft.AspNetCore.Authorization;

namespace Callora.Host.Backend.Api;

public static class PluginEndpoints
{
    public static RouteGroupBuilder MapPluginEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/plugins")
            .WithTags("Plugins")
            .RequireAuthorization();

        group.MapGet("/", (IPluginLifecycleService lifecycleService) => Results.Ok(lifecycleService.Plugins))
            .WithName("Plugins_List");

        group.MapGet("/installed", async (
            IPluginLifecycleService lifecycleService,
            CancellationToken cancellationToken) =>
        {
            var installations = await lifecycleService.GetInstallationsAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(installations);
        }).WithName("Plugins_InstalledList");

        group.MapGet("/audit", async (
            int? take,
            IHostAuditStore auditStore,
            CancellationToken cancellationToken) =>
        {
            var entries = await auditStore.GetRecentAsync(take ?? 200, cancellationToken).ConfigureAwait(false);
            return Results.Ok(entries);
        }).WithName("Plugins_AuditList");

        group.MapPost("/install", async (
            InstallPluginRequest request,
            IPluginLifecycleService lifecycleService,
            CancellationToken cancellationToken) =>
        {
            var result = await lifecycleService.InstallAsync(
                    new InstallPluginCommand(request.AssemblyPath, request.EntryTypeName, request.RequestedBy),
                    cancellationToken)
                .ConfigureAwait(false);

            return ToHttpResult(result);
        }).WithName("Plugins_Install");

        group.MapPost("/install/nuget", async (
            InstallNuGetPluginRequest request,
            IPluginLifecycleService lifecycleService,
            CancellationToken cancellationToken) =>
        {
            var result = await lifecycleService.InstallFromNuGetAsync(
                    new InstallNuGetPluginCommand(
                        request.PackageId,
                        request.PackageVersion,
                        request.AssemblyFileName,
                        request.EntryTypeName,
                        request.RequestedBy),
                    cancellationToken)
                .ConfigureAwait(false);

            return ToHttpResult(result);
        }).WithName("Plugins_InstallFromNuGet");

        group.MapPost("/{pluginId}/activate", async (
            string pluginId,
            PluginLifecycleRequest request,
            IPluginLifecycleService lifecycleService,
            CancellationToken cancellationToken) =>
        {
            var result = await lifecycleService.ActivateAsync(
                    new PluginLifecycleCommand(pluginId, request.RequestedBy),
                    cancellationToken)
                .ConfigureAwait(false);

            return ToHttpResult(result);
        }).WithName("Plugins_Activate");

        group.MapPost("/{pluginId}/deactivate", async (
            string pluginId,
            PluginLifecycleRequest request,
            IPluginLifecycleService lifecycleService,
            CancellationToken cancellationToken) =>
        {
            var result = await lifecycleService.DeactivateAsync(
                    new PluginLifecycleCommand(pluginId, request.RequestedBy),
                    cancellationToken)
                .ConfigureAwait(false);

            return ToHttpResult(result);
        }).WithName("Plugins_Deactivate");

        group.MapDelete("/{pluginId}", async (
            string pluginId,
            string? requestedBy,
            IPluginLifecycleService lifecycleService,
            CancellationToken cancellationToken) =>
        {
            var result = await lifecycleService.UninstallAsync(
                    new PluginLifecycleCommand(pluginId, requestedBy),
                    cancellationToken)
                .ConfigureAwait(false);

            return ToHttpResult(result);
        }).WithName("Plugins_Uninstall");

        return group;
    }

    private static IResult ToHttpResult(PluginLifecycleServiceResult result)
    {
        var response = new PluginLifecycleApiResponse(result.IsSuccess, result.PluginId, result.Message);

        return result.Status switch
        {
            PluginLifecycleServiceStatus.Ok => Results.Ok(response),
            PluginLifecycleServiceStatus.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
            _ => Results.BadRequest(response),
        };
    }
}
