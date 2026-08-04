using Callora.Core.Api;
using Callora.Core.Application.Extensions;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Application.Workspaces;
using Callora.Core.Infrastructure.Security;
using System.Text.Json;

namespace Callora.Administration.Api;

/// <summary>
/// Per-surface theming: a surface may run its workspace's theme with its own
/// values, or a different theme entirely — the level below the workspace, in the
/// sense a sales channel has its own look while sharing the shop's baseline.
/// </summary>
public static class SurfaceThemeEndpoints
{
    public static void MapSurfaceThemeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/themes/workspaces/{workspaceKey}/surfaces/{surfaceKey}")
            .WithTags("Themes")
            .RequireAuthorization();

        group.MapGet("", async (
            string workspaceKey,
            string surfaceKey,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            SurfaceThemeService service,
            CancellationToken cancellationToken) =>
        {
            if (!await IsInConfiguredTenantAsync(workspaceKey, hostOptions, workspaceStore, cancellationToken)
                    .ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var result = await service.GetAssignmentAsync(workspaceKey, surfaceKey, cancellationToken)
                .ConfigureAwait(false);
            return result.Status == SurfaceThemeStatus.Ok && result.Assignment is not null
                ? Results.Ok(ToResponse(result.Assignment))
                : ToProblem(result.Status, result.Message);
        }).WithName("Themes_Surface_Get")
            .RequirePermission(BackendPermissionKeys.ExtensionRead);

        group.MapPut("", async (
            string workspaceKey,
            string surfaceKey,
            SurfaceThemeAssignmentUpsertApiRequest request,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            SurfaceThemeService service,
            IWorkspaceTemplateResolutionCache cache,
            CancellationToken cancellationToken) =>
        {
            if (!await IsInConfiguredTenantAsync(workspaceKey, hostOptions, workspaceStore, cancellationToken)
                    .ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            if (string.IsNullOrWhiteSpace(request.ThemePluginId) || string.IsNullOrWhiteSpace(request.ThemeVersion))
            {
                return ApiProblems.BadRequest("themePluginId and themeVersion are required.");
            }

            var result = await service
                .AssignAsync(workspaceKey, surfaceKey, request.ThemePluginId, request.ThemeVersion, cancellationToken)
                .ConfigureAwait(false);
            if (result.Status != SurfaceThemeStatus.Ok || result.Assignment is null)
            {
                return ToProblem(result.Status, result.Message);
            }

            cache.InvalidateWorkspace(workspaceKey);
            return Results.Ok(ToResponse(result.Assignment));
        }).WithName("Themes_Surface_Upsert")
            .RequirePermission(BackendPermissionKeys.ExtensionUpdate);

        group.MapDelete("", async (
            string workspaceKey,
            string surfaceKey,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            SurfaceThemeService service,
            IWorkspaceTemplateResolutionCache cache,
            CancellationToken cancellationToken) =>
        {
            if (!await IsInConfiguredTenantAsync(workspaceKey, hostOptions, workspaceStore, cancellationToken)
                    .ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var result = await service.ClearAsync(workspaceKey, surfaceKey, cancellationToken).ConfigureAwait(false);
            if (result.Status != SurfaceThemeStatus.Ok || result.Assignment is null)
            {
                return ToProblem(result.Status, result.Message);
            }

            cache.InvalidateWorkspace(workspaceKey);
            return Results.Ok(ToResponse(result.Assignment));
        }).WithName("Themes_Surface_Delete")
            .RequirePermission(BackendPermissionKeys.ExtensionUpdate);

        group.MapGet("/settings", async (
            string workspaceKey,
            string surfaceKey,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            SurfaceThemeService service,
            CancellationToken cancellationToken) =>
        {
            if (!await IsInConfiguredTenantAsync(workspaceKey, hostOptions, workspaceStore, cancellationToken)
                    .ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var result = await service.GetSettingsAsync(workspaceKey, surfaceKey, cancellationToken)
                .ConfigureAwait(false);
            return result.Status == SurfaceThemeStatus.Ok && result.Settings is not null
                ? Results.Ok(ToResponse(result.Settings))
                : ToProblem(result.Status, result.Message);
        }).WithName("Themes_Surface_Settings_Get")
            .RequirePermission(BackendPermissionKeys.ExtensionRead);

        group.MapPut("/settings", async (
            string workspaceKey,
            string surfaceKey,
            UpsertWorkspaceThemeSettingsApiRequest request,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            SurfaceThemeService service,
            IWorkspaceTemplateResolutionCache cache,
            CancellationToken cancellationToken) =>
        {
            if (!await IsInConfiguredTenantAsync(workspaceKey, hostOptions, workspaceStore, cancellationToken)
                    .ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var result = await service
                .ReplaceSettingsAsync(workspaceKey, surfaceKey, ToValueMap(request), cancellationToken)
                .ConfigureAwait(false);
            if (result.Status != SurfaceThemeStatus.Ok || result.Settings is null)
            {
                return ToProblem(result.Status, result.Message);
            }

            cache.InvalidateWorkspace(workspaceKey);
            return Results.Ok(ToResponse(result.Settings));
        }).WithName("Themes_Surface_Settings_Upsert")
            .RequirePermission(BackendPermissionKeys.ExtensionUpdate);
    }

    // A JSON null means "remove this override" — the value then falls through to
    // the workspace, and from there to the theme default.
    private static Dictionary<string, string?> ToValueMap(UpsertWorkspaceThemeSettingsApiRequest request)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in request.ValuesByKey ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                continue;
            }

            values[pair.Key.Trim()] = pair.Value.ValueKind == JsonValueKind.Null ? null : pair.Value.GetRawText();
        }

        return values;
    }

    private static SurfaceThemeAssignmentApiResponse ToResponse(SurfaceThemeAssignment assignment) =>
        new(
            assignment.WorkspaceKey,
            assignment.SurfaceKey,
            assignment.ThemePluginId,
            assignment.ThemeVersion,
            assignment.InheritedFromWorkspace);

    private static SurfaceThemeSettingsApiResponse ToResponse(SurfaceThemeSettings settings) =>
        new(
            settings.WorkspaceKey,
            settings.SurfaceKey,
            settings.HasAssignedTheme,
            settings.ThemePluginId,
            settings.ThemeVersion,
            settings.InheritedFromWorkspace,
            settings.InheritsWorkspaceValues,
            settings.Fields.Select(ToResponse).ToArray(),
            settings.OwnValuesByKey,
            settings.InheritedValuesByKey);

    private static WorkspaceThemeSettingDefinitionApiResponse ToResponse(
        WorkspaceThemeSettingDefinitionSnapshot definition) =>
        new(
            definition.SettingKey,
            definition.Label,
            definition.FieldType,
            definition.Description,
            definition.DefaultValueJson,
            definition.IsRequired,
            definition.SortOrder,
            definition.GroupName,
            definition.OptionsJson,
            definition.IsActive);

    private static IResult ToProblem(SurfaceThemeStatus status, string? message) => status switch
    {
        SurfaceThemeStatus.WorkspaceNotFound or SurfaceThemeStatus.SurfaceNotFound =>
            ApiProblems.NotFound(message ?? "Not found."),
        _ => ApiProblems.BadRequest(message ?? "The request could not be processed."),
    };

    private static async Task<bool> IsInConfiguredTenantAsync(
        string workspaceKey,
        BackendHostOptions hostOptions,
        IWorkspaceManagementStore workspaceStore,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(hostOptions.DefaultTenantKey))
        {
            return false;
        }

        var workspace = await workspaceStore.GetAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        return workspace is not null &&
               string.Equals(workspace.TenantKey, hostOptions.DefaultTenantKey, StringComparison.OrdinalIgnoreCase);
    }
}
