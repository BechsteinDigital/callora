using Callora.Core.Api;
using Callora.Core.Application.Extensions;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Security;
using Callora.Core.Application.Workspaces;
using Callora.Core.Infrastructure.Security;
using System.Text.Json;

namespace Callora.Administration.Api;

public static class ThemeEndpoints
{
    public static void MapThemeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/themes")
            .WithTags("Themes")
            .RequireAuthorization();

        group.MapGet("/definitions", async (
            string? surface,
            bool? active,
            IWorkspaceTemplateRegistryStore store,
            CancellationToken cancellationToken) =>
        {
            var definitions = await store
                .ListDefinitionsAsync(surface, active, cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(definitions.Select(ToDefinitionResponse).ToArray());
        }).WithName("Themes_Definitions_List")
            .RequirePermission(BackendPermissionKeys.ExtensionRead);

        group.MapPut("/definitions/{templateKey}/plugins/{pluginId}/versions/{version}", async (
            string templateKey,
            string pluginId,
            string version,
            ThemeDefinitionUpsertApiRequest request,
            IWorkspaceTemplateRegistryStore store,
            IWorkspaceTemplateResolutionCache cache,
            CancellationToken cancellationToken) =>
        {
            var definition = await store
                .UpsertDefinitionAsync(
                    templateKey,
                    request.Surface,
                    pluginId,
                    version,
                    request.DisplayName,
                    request.TemplatePath,
                    request.ParentTemplateKey,
                    request.Scope,
                    request.IsActive,
                    request.Priority,
                    cancellationToken)
                .ConfigureAwait(false);

            cache.InvalidateAll();
            return Results.Ok(ToDefinitionResponse(definition));
        }).WithName("Themes_Definitions_Upsert")
            .RequirePermission(BackendPermissionKeys.ExtensionUpdate);

        group.MapPut("/definitions/{templateKey}/plugins/{pluginId}/versions/{version}/activation", async (
            string templateKey,
            string pluginId,
            string version,
            ThemeActivationApiRequest request,
            IWorkspaceTemplateRegistryStore store,
            IWorkspaceTemplateResolutionCache cache,
            CancellationToken cancellationToken) =>
        {
            var updated = await store
                .SetDefinitionActivationAsync(templateKey, pluginId, version, request.IsActive, cancellationToken)
                .ConfigureAwait(false);
            if (!updated)
            {
                return Results.NotFound();
            }

            cache.InvalidateAll();
            return Results.NoContent();
        }).WithName("Themes_Definitions_SetActivation")
            .RequirePermission(BackendPermissionKeys.ExtensionUpdate);

        group.MapGet("/workspaces/{workspaceKey}", async (
            string workspaceKey,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            CancellationToken cancellationToken) =>
        {
            if (!await IsWorkspaceInConfiguredTenantAsync(workspaceKey, hostOptions, workspaceStore, cancellationToken).ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var assignment = await workspaceStore.GetThemeAssignmentAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
            return assignment is null ? Results.NotFound() : Results.Ok(ToThemeAssignmentResponse(assignment));
        }).WithName("Themes_Workspace_Get")
            .RequirePermission(BackendPermissionKeys.ExtensionRead);

        group.MapPut("/workspaces/{workspaceKey}", async (
            string workspaceKey,
            WorkspaceThemeAssignmentUpsertApiRequest request,
            BackendHostOptions hostOptions,
            IWorkspaceTemplateRegistryStore store,
            IWorkspaceManagementStore workspaceStore,
            IWorkspaceTemplateResolutionCache cache,
            CancellationToken cancellationToken) =>
        {
            if (!await IsWorkspaceInConfiguredTenantAsync(workspaceKey, hostOptions, workspaceStore, cancellationToken).ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var definitionExists = (await store.ListDefinitionsAsync(surface: "workspace", isActive: true, cancellationToken)
                    .ConfigureAwait(false))
                .Any(x =>
                    string.Equals(x.PluginId, request.ThemePluginId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.Version, request.ThemeVersion, StringComparison.OrdinalIgnoreCase));

            if (!definitionExists)
            {
                return Results.BadRequest(new
                {
                    message = $"No active workspace theme definitions found for {request.ThemePluginId}@{request.ThemeVersion}."
                });
            }

            var assignment = await workspaceStore
                .UpsertThemeAssignmentAsync(
                    workspaceKey,
                    request.ThemePluginId,
                    request.ThemeVersion,
                    request.AssignedBy,
                    cancellationToken)
                .ConfigureAwait(false);

            if (assignment is null)
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            cache.InvalidateWorkspace(workspaceKey);
            return Results.Ok(ToThemeAssignmentResponse(assignment));
        }).WithName("Themes_Workspace_Upsert")
            .RequirePermission(BackendPermissionKeys.ExtensionUpdate);

        group.MapDelete("/workspaces/{workspaceKey}", async (
            string workspaceKey,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            IWorkspaceTemplateResolutionCache cache,
            CancellationToken cancellationToken) =>
        {
            if (!await IsWorkspaceInConfiguredTenantAsync(workspaceKey, hostOptions, workspaceStore, cancellationToken).ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var cleared = await workspaceStore.ClearThemeAssignmentAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
            if (!cleared)
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            cache.InvalidateWorkspace(workspaceKey);
            return Results.NoContent();
        }).WithName("Themes_Workspace_Delete")
            .RequirePermission(BackendPermissionKeys.ExtensionUpdate);

        group.MapGet("/workspaces/{workspaceKey}/effective", async (
            string workspaceKey,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            IWorkspaceTemplateResolutionService resolver,
            CancellationToken cancellationToken) =>
        {
            if (!await IsWorkspaceInConfiguredTenantAsync(workspaceKey, hostOptions, workspaceStore, cancellationToken).ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var effective = await resolver.ResolveAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
            return Results.Ok(effective.Select(ToEffectiveResponse).ToArray());
        }).WithName("Themes_Workspace_Effective")
            .RequirePermission(BackendPermissionKeys.ExtensionRead);

        group.MapGet("/workspaces/{workspaceKey}/settings", async (
            string workspaceKey,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            IWorkspaceThemeSettingsStore settingsStore,
            CancellationToken cancellationToken) =>
        {
            if (!await IsWorkspaceInConfiguredTenantAsync(workspaceKey, hostOptions, workspaceStore, cancellationToken).ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var assignment = await workspaceStore.GetThemeAssignmentAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
            if (assignment is null ||
                string.IsNullOrWhiteSpace(assignment.ThemePluginId) ||
                string.IsNullOrWhiteSpace(assignment.ThemeVersion))
            {
                return Results.Ok(new WorkspaceThemeSettingsApiResponse(
                    WorkspaceKey: workspaceKey,
                    HasAssignedTheme: false,
                    ThemePluginId: null,
                    ThemeVersion: null,
                    Fields: Array.Empty<WorkspaceThemeSettingDefinitionApiResponse>(),
                    ValuesByKey: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
            }

            var definitions = await settingsStore
                .ListDefinitionsAsync(assignment.ThemePluginId, assignment.ThemeVersion, cancellationToken)
                .ConfigureAwait(false);
            var values = await settingsStore
                .ListValuesAsync(workspaceKey, surfaceKey: null, assignment.ThemePluginId, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(new WorkspaceThemeSettingsApiResponse(
                WorkspaceKey: workspaceKey,
                HasAssignedTheme: true,
                ThemePluginId: assignment.ThemePluginId,
                ThemeVersion: assignment.ThemeVersion,
                Fields: definitions.Select(ToSettingsResponse).ToArray(),
                ValuesByKey: values.ToDictionary(x => x.SettingKey, x => x.ValueJson, StringComparer.OrdinalIgnoreCase)));
        }).WithName("Themes_Workspace_Settings_Get")
            .RequirePermission(BackendPermissionKeys.ExtensionRead);

        group.MapPut("/workspaces/{workspaceKey}/settings", async (
            string workspaceKey,
            UpsertWorkspaceThemeSettingsApiRequest request,
            BackendHostOptions hostOptions,
            IWorkspaceManagementStore workspaceStore,
            IWorkspaceThemeSettingsStore settingsStore,
            CancellationToken cancellationToken) =>
        {
            if (!await IsWorkspaceInConfiguredTenantAsync(workspaceKey, hostOptions, workspaceStore, cancellationToken).ConfigureAwait(false))
            {
                return ApiProblems.NotFound($"Workspace '{workspaceKey}' not found.");
            }

            var assignment = await workspaceStore.GetThemeAssignmentAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
            if (assignment is null ||
                string.IsNullOrWhiteSpace(assignment.ThemePluginId) ||
                string.IsNullOrWhiteSpace(assignment.ThemeVersion))
            {
                return ApiProblems.BadRequest("No theme assigned to this workspace.");
            }

            var definitions = await settingsStore
                .ListDefinitionsAsync(assignment.ThemePluginId, assignment.ThemeVersion, cancellationToken)
                .ConfigureAwait(false);
            var allowedKeys = definitions
                .Where(x => x.IsActive)
                .Select(x => x.SettingKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var normalizedInput = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in request.ValuesByKey ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                var settingKey = pair.Key.Trim();
                if (!allowedKeys.Contains(settingKey))
                {
                    continue;
                }

                normalizedInput[settingKey] = pair.Value.ValueKind == JsonValueKind.Null
                    ? null
                    : pair.Value.GetRawText();
            }

            var values = await settingsStore
                .ReplaceValuesAsync(workspaceKey, surfaceKey: null, assignment.ThemePluginId, normalizedInput, cancellationToken)
                .ConfigureAwait(false);

            return Results.Ok(new WorkspaceThemeSettingsApiResponse(
                WorkspaceKey: workspaceKey,
                HasAssignedTheme: true,
                ThemePluginId: assignment.ThemePluginId,
                ThemeVersion: assignment.ThemeVersion,
                Fields: definitions.Select(ToSettingsResponse).ToArray(),
                ValuesByKey: values.ToDictionary(x => x.SettingKey, x => x.ValueJson, StringComparer.OrdinalIgnoreCase)));
        }).WithName("Themes_Workspace_Settings_Upsert")
            .RequirePermission(BackendPermissionKeys.ExtensionUpdate);
    }

    private static ThemeDefinitionApiResponse ToDefinitionResponse(WorkspaceTemplateDefinitionSnapshot definition)
    {
        return new ThemeDefinitionApiResponse(
            definition.TemplateKey,
            definition.Surface,
            definition.PluginId,
            definition.Version,
            definition.DisplayName,
            definition.TemplatePath,
            definition.ParentTemplateKey,
            definition.Scope,
            definition.IsActive,
            definition.Priority,
            definition.CreatedAtUtc,
            definition.UpdatedAtUtc);
    }

    private static WorkspaceThemeAssignmentApiResponse ToThemeAssignmentResponse(WorkspaceThemeAssignmentSnapshot assignment)
    {
        return new WorkspaceThemeAssignmentApiResponse(
            assignment.WorkspaceKey,
            assignment.ThemePluginId,
            assignment.ThemeVersion,
            assignment.AssignedBy,
            assignment.AssignedAtUtc);
    }

    private static WorkspaceTemplateEffectiveApiResponse ToEffectiveResponse(WorkspaceTemplateEffectiveSnapshot effective)
    {
        return new WorkspaceTemplateEffectiveApiResponse(
            effective.TenantKey,
            effective.WorkspaceKey,
            effective.TemplateKey,
            effective.Surface,
            effective.PluginId,
            effective.Version,
            effective.DisplayName,
            effective.TemplatePath,
            effective.ParentTemplateKey,
            effective.Scope,
            effective.Source,
            effective.Priority);
    }

    private static WorkspaceThemeSettingDefinitionApiResponse ToSettingsResponse(WorkspaceThemeSettingDefinitionSnapshot definition)
    {
        return new WorkspaceThemeSettingDefinitionApiResponse(
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
    }

    private static async Task<bool> IsWorkspaceInConfiguredTenantAsync(
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
        if (workspace is null)
        {
            return false;
        }

        return string.Equals(workspace.TenantKey, hostOptions.DefaultTenantKey, StringComparison.OrdinalIgnoreCase);
    }
}
