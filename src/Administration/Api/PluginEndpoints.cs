using Callora.Core.Api;
using Callora.Core.Application.Audit;
using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Application.Startup;
using Callora.Core.Application.Workspaces;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Callora.Administration.Api;

public static class PluginEndpoints
{
    public static RouteGroupBuilder MapPluginEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/plugins")
            .WithTags("Plugins")
            .RequireAuthorization();

        group.MapGet("/", (IPluginLifecycleService lifecycleService) => Results.Ok(lifecycleService.Plugins))
            .WithName("Plugins_List")
            .RequirePermission(BackendPermissionKeys.PluginRead);

        group.MapGet("/installed", async (
            System.Security.Claims.ClaimsPrincipal user,
            [FromServices] IPluginDiscoveryService discovery,
            [FromServices] IPluginLifecycleService lifecycleService,
            CancellationToken cancellationToken) =>
        {
            // Reconcile the registry against the local plugin directories before listing,
            // so the overview reflects the file system without a separate refresh call
            // (Shopware plugin-list behaviour). Reconciliation writes (register/update/
            // uninstall), so it runs only for callers who may modify the plugin registry
            // (PluginCreate); read-only operators get the current list without side effects.
            if (EndpointAuthorizationExtensions.UserHasPermission(user, BackendPermissionKeys.PluginCreate))
            {
                await discovery.RefreshAsync(cancellationToken).ConfigureAwait(false);
            }

            var installations = await lifecycleService.GetInstallationsAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(installations);
        }).WithName("Plugins_InstalledList")
            .RequirePermission(BackendPermissionKeys.PluginRead);

        group.MapGet("/signature-report", async (
            [FromServices] IPluginLifecycleService lifecycleService,
            [FromServices] IPluginPackageSignatureVerifier signatureVerifier,
            CancellationToken cancellationToken) =>
        {
            // Re-verify each installed plugin so the admin sees its CURRENT signature
            // standing (signed-trusted / unsigned / untrusted / revoked / hash-mismatch),
            // not a value cached at install time.
            var installations = await lifecycleService.GetInstallationsAsync(cancellationToken).ConfigureAwait(false);
            var report = new List<PluginSignatureStatusApiResponse>(installations.Count);
            foreach (var installation in installations)
            {
                var verification = await signatureVerifier
                    .VerifyAsync(installation.AssemblyPath, cancellationToken)
                    .ConfigureAwait(false);
                report.Add(new PluginSignatureStatusApiResponse(
                    installation.PluginId,
                    PluginSignatureStateMapper.Map(verification),
                    verification.SignerThumbprint));
            }

            return Results.Ok(report);
        })
            .WithName("Plugins_SignatureReport")
            .WithSummary("Reports each installed plugin's current signature state")
            .RequirePermission(BackendPermissionKeys.PluginRead);

        group.MapGet("/audit", async (
            int? take,
            IHostAuditStore auditStore,
            CancellationToken cancellationToken) =>
        {
            var entries = await auditStore.GetRecentAsync(take ?? 200, cancellationToken).ConfigureAwait(false);
            return Results.Ok(entries);
        }).WithName("Plugins_AuditList")
            .RequirePermission(BackendPermissionKeys.PluginRead);

        group.MapGet("/contracts/support", () =>
        {
            var contracts = PluginContractVersionPolicy.GetAll()
                .Select(x => new PluginContractSupportApiResponse(
                    x.ContractVersion,
                    x.Status.ToString(),
                    x.Status is not PluginContractSupportStatus.Removed,
                    x.Status is PluginContractSupportStatus.Deprecated,
                    x.Message))
                .ToArray();

            return Results.Ok(contracts);
        })
            .WithName("Plugins_ContractsSupport")
            .WithSummary("Lists contract support status")
            .WithDescription("Returns support status for each known plugin contract version (supported, deprecated, removed).")
            .RequirePermission(BackendPermissionKeys.PluginRead);

        group.MapGet("/contracts/compatibility", () =>
        {
            var hostVersion = GetAssemblyVersion(typeof(PluginEndpoints).Assembly);
            var coreVersion = GetAssemblyVersion(typeof(CalloraHostStartup).Assembly);

            var matrix = PluginContractVersionPolicy.GetAll()
                .Select(x => new PluginContractCompatibilityApiResponse(
                    hostVersion,
                    coreVersion,
                    x.ContractVersion,
                    GetCompatibilityResult(x.Status),
                    x.Status is not PluginContractSupportStatus.Removed,
                    x.Message))
                .ToArray();

            return Results.Ok(matrix);
        })
            .WithName("Plugins_ContractsCompatibility")
            .WithSummary("Lists host/core/plugin contract compatibility matrix")
            .WithDescription("Returns one compatibility row per known plugin contract version.")
            .RequirePermission(BackendPermissionKeys.PluginRead);

        group.MapGet("/security/trusted-signers", (IPluginSignatureTrustStore trustStore) =>
        {
            var signers = trustStore.GetTrustedSigners()
                .Select(x => new TrustedPluginSignerApiResponse(
                    x.PublisherId,
                    x.DisplayName,
                    x.Thumbprint,
                    x.Source))
                .ToArray();

            return Results.Ok(signers);
        })
            .WithName("Plugins_TrustedSigners")
            .WithSummary("Lists trusted plugin signers")
            .WithDescription("Returns trusted signer thumbprints with publisher metadata for signature verification.")
            .RequirePermission(BackendPermissionKeys.PluginRead);

        group.MapGet("/workspaces/{workspaceKey}/entitlements/{pluginId}", async (
            string workspaceKey,
            string pluginId,
            BackendHostOptions hostOptions,
            IPluginEntitlementStore entitlementStore,
            [FromServices] IWorkspaceManagementStore workspaceStore,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hostOptions.DefaultTenantKey))
            {
                return ApiProblems.BadRequest("BackendHost.DefaultTenantKey is not configured.");
            }

            var workspace = await workspaceStore.GetAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
            if (workspace is null ||
                !string.Equals(workspace.TenantKey, hostOptions.DefaultTenantKey, StringComparison.OrdinalIgnoreCase))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var isEntitled = await entitlementStore
                .IsEntitledAsync(pluginId, workspace.WorkspaceKey, workspace.TenantKey, cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(new PluginWorkspaceEntitlementApiResponse(
                workspace.WorkspaceKey,
                pluginId,
                isEntitled,
                workspace.TenantKey));
        })
            .WithName("Plugins_WorkspaceEntitlementStatus")
            .WithSummary("Gets workspace-specific plugin runtime activation status")
            .WithDescription("Returns whether one workspace currently has one plugin enabled at runtime.")
            .RequirePermission(BackendPermissionKeys.PluginRead);

        group.MapGet("/tenants/{tenantId}/entitlements/{pluginId}", async (
            string tenantId,
            string pluginId,
            BackendHostOptions hostOptions,
            IPluginEntitlementStore entitlementStore,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(hostOptions.DefaultTenantKey))
            {
                return ApiProblems.BadRequest("BackendHost.DefaultTenantKey is not configured.");
            }

            if (!string.Equals(tenantId, hostOptions.DefaultTenantKey, StringComparison.OrdinalIgnoreCase))
            {
                return ApiProblems.NotFound($"Tenant '{tenantId}' not found.");
            }

            var isEntitled = await entitlementStore
                .IsEntitledAsync(pluginId, workspaceKey: null, tenantKey: hostOptions.DefaultTenantKey, cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(new PluginWorkspaceEntitlementApiResponse(hostOptions.DefaultTenantKey, pluginId, isEntitled, hostOptions.DefaultTenantKey));
        })
            .WithSummary("Gets workspace-specific plugin runtime activation status (legacy tenant route)")
            .WithDescription("Legacy route alias. Prefer /workspaces/{workspaceKey}/entitlements/{pluginId}.")
            .RequirePermission(BackendPermissionKeys.PluginRead);

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
        }).WithName("Plugins_Install")
            .RequirePermission(BackendPermissionKeys.PluginCreate);

        group.MapPost("/install/local", async (
            InstallLocalPluginRequest request,
            [FromServices] ILocalPluginInstallSourceResolver localResolver,
            IPluginLifecycleService lifecycleService,
            CancellationToken cancellationToken) =>
        {
            var source = await localResolver
                .ResolveForInstallAsync(request.PluginId, request.BuildIfNeeded, request.ForceBuild, cancellationToken)
                .ConfigureAwait(false);
            if (!source.IsSuccess || string.IsNullOrWhiteSpace(source.AssemblyPath))
            {
                return ToHttpResult(new PluginLifecycleServiceResult(
                    PluginLifecycleServiceStatus.BadRequest,
                    false,
                    source.PluginId,
                    source.Message,
                    source.ErrorCode));
            }

            var result = await lifecycleService.InstallAsync(
                    new InstallPluginCommand(
                        source.AssemblyPath,
                        source.EntryTypeName,
                        request.RequestedBy),
                    cancellationToken)
                .ConfigureAwait(false);

            return ToHttpResult(result);
        }).WithName("Plugins_InstallLocal")
            .RequirePermission(BackendPermissionKeys.PluginCreate);

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
        }).WithName("Plugins_InstallFromNuGet")
            .RequirePermission(BackendPermissionKeys.PluginCreate);

        group.MapPost("/{pluginId}/update/nuget", async (
            string pluginId,
            UpdateNuGetPluginRequest request,
            IPluginLifecycleService lifecycleService,
            CancellationToken cancellationToken) =>
        {
            var result = await lifecycleService.UpdateFromNuGetAsync(
                    new UpdateNuGetPluginCommand(
                        pluginId,
                        request.PackageId,
                        request.PackageVersion,
                        request.AssemblyFileName,
                        request.EntryTypeName,
                        request.RequestedBy),
                cancellationToken)
                .ConfigureAwait(false);

            return ToHttpResult(result);
        }).WithName("Plugins_UpdateFromNuGet")
            .RequirePermission(BackendPermissionKeys.PluginCreate);

        group.MapPost("/{pluginId}/update/local", async (
            string pluginId,
            UpdateLocalPluginRequest request,
            IPluginLifecycleService lifecycleService,
            CancellationToken cancellationToken) =>
        {
            var result = await lifecycleService.UpdateFromLocalAsync(
                    new UpdateLocalPluginCommand(
                        pluginId,
                        request.BuildIfNeeded,
                        request.ForceBuild,
                        request.RequestedBy),
                    cancellationToken)
                .ConfigureAwait(false);

            return ToHttpResult(result);
        }).WithName("Plugins_UpdateFromLocal")
            .RequirePermission(BackendPermissionKeys.PluginCreate);

        // Nullable statt pflichtig: Ohne Body macht ASP.NET aus der fehlenden Bindung eine
        // BadHttpRequestException — der Aufrufer bekommt 500 mit Stapelverfolgung für einen
        // Fehler, den er selbst gemacht hat. Ein 400, das sagt was fehlt, ist die Antwort.
        group.MapPost("/{pluginId}/activate", async (
            string pluginId,
            PluginLifecycleRequest? request,
            IPluginLifecycleService lifecycleService,
            CancellationToken cancellationToken) =>
        {
            if (request is null)
            {
                return ApiProblems.BadRequest(
                    "A request body with 'requestedBy' is required — it is what the audit trail records.");
            }

            var result = await lifecycleService.ActivateAsync(
                    new PluginLifecycleCommand(pluginId, request.RequestedBy, request.WorkspaceKey),
                    cancellationToken)
                .ConfigureAwait(false);

            return ToHttpResult(result);
        }).WithName("Plugins_Activate")
            .RequirePermission(BackendPermissionKeys.PluginExecute);

        // Nullable statt pflichtig: Ohne Body macht ASP.NET aus der fehlenden Bindung eine
        // BadHttpRequestException — der Aufrufer bekommt 500 mit Stapelverfolgung für einen
        // Fehler, den er selbst gemacht hat. Ein 400, das sagt was fehlt, ist die Antwort.
        group.MapPost("/{pluginId}/deactivate", async (
            string pluginId,
            PluginLifecycleRequest? request,
            IPluginLifecycleService lifecycleService,
            CancellationToken cancellationToken) =>
        {
            if (request is null)
            {
                return ApiProblems.BadRequest(
                    "A request body with 'requestedBy' is required — it is what the audit trail records.");
            }

            var result = await lifecycleService.DeactivateAsync(
                    new PluginLifecycleCommand(pluginId, request.RequestedBy, request.WorkspaceKey),
                    cancellationToken)
                .ConfigureAwait(false);

            return ToHttpResult(result);
        }).WithName("Plugins_Deactivate")
            .RequirePermission(BackendPermissionKeys.PluginExecute);

        group.MapDelete("/{pluginId}", async (
            string pluginId,
            string? requestedBy,
            IPluginLifecycleService lifecycleService,
            CancellationToken cancellationToken) =>
        {
            var result = await lifecycleService.UninstallAsync(
                    new PluginLifecycleCommand(pluginId, requestedBy, null),
                    cancellationToken)
                .ConfigureAwait(false);

            return ToHttpResult(result);
        }).WithName("Plugins_Uninstall")
            .RequirePermission(BackendPermissionKeys.PluginDelete);

        return group;
    }

    private static IResult ToHttpResult(PluginLifecycleServiceResult result)
    {
        var response = new PluginLifecycleApiResponse(
            result.IsSuccess,
            result.PluginId,
            result.Message,
            result.ErrorCode,
            result.WarningMessage,
            result.WarningCode);

        return result.Status switch
        {
            PluginLifecycleServiceStatus.Ok => Results.Ok(response),
            PluginLifecycleServiceStatus.Forbidden => Results.Json(response, statusCode: StatusCodes.Status403Forbidden),
            _ => Results.BadRequest(response),
        };
    }

    private static string GetCompatibilityResult(PluginContractSupportStatus status) =>
        status switch
        {
            PluginContractSupportStatus.Supported => "compatible",
            PluginContractSupportStatus.Deprecated => "compatible_with_warning",
            PluginContractSupportStatus.Removed => "incompatible",
            _ => "unknown"
        };

    private static string GetAssemblyVersion(System.Reflection.Assembly assembly)
    {
        var informationalVersion = assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), inherit: false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "unknown";
    }
}
