using System.Security.Claims;
using Callora.Host.Backend.Application.Abstractions;
using Callora.Host.Backend.Application.Abstractions.Integrations;
using Callora.Host.Backend.Application.Abstractions.Security;
using Callora.Host.Backend.Application.Audit;
using Callora.Host.Backend.Application.Integrations;
using Callora.Host.Backend.Domain.Integrations;
using Callora.Host.Backend.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace Callora.Host.Backend.Api;

/// <summary>
/// Management API for named machine-to-machine integrations (PLAT-264). Each
/// integration carries its own RBAC role and scope instead of the global,
/// super-admin bootstrap key; creation and revocation are audited.
/// </summary>
public static class IntegrationEndpoints
{
    public static void MapIntegrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/security/integrations")
            .WithTags("Security")
            .RequireAuthorization();

        group.MapGet("/", async ([FromServices] IIntegrationCredentialStore store, CancellationToken cancellationToken) =>
        {
            var items = (await store.ListAsync(cancellationToken).ConfigureAwait(false))
                .Select(ToResponse)
                .ToArray();
            return Results.Ok(items);
        })
            .WithName("Integrations_List")
            .Produces<IntegrationApiResponse[]>()
            .RequirePermission(BackendPermissionKeys.IntegrationRead);

        group.MapPost("/", async (
            IntegrationCreateApiRequest request,
            ClaimsPrincipal user,
            [FromServices] IIntegrationCredentialStore store,
            [FromServices] IBackendRbacStore rbacStore,
            [FromServices] IHostAuditStore auditStore,
            CancellationToken cancellationToken) =>
        {
            var name = request.Name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                return Results.BadRequest(new { error = "name is required." });

            var role = request.Role?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(role))
                return Results.BadRequest(new { error = "role is required." });

            // Operator roles would defeat the bounded-access purpose of an integration.
            if (string.Equals(role, BackendRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, BackendRoles.HostApi, StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "Integrations may not use operator roles." });
            }

            var knownPermissions = await rbacStore.GetRolePermissionsAsync(role, cancellationToken).ConfigureAwait(false);
            if (knownPermissions is null)
                return Results.BadRequest(new { error = $"Unknown RBAC role '{role}'." });

            var scope = string.IsNullOrWhiteSpace(request.Scope)
                ? BackendAuthScopes.Platform
                : request.Scope.Trim().ToLowerInvariant();
            if (scope != BackendAuthScopes.Platform && scope != BackendAuthScopes.Workspace)
                return Results.BadRequest(new { error = "scope must be 'platform' or 'workspace'." });

            var workspaceKey = request.WorkspaceKey?.Trim();
            if (scope == BackendAuthScopes.Workspace && string.IsNullOrWhiteSpace(workspaceKey))
                return Results.BadRequest(new { error = "workspaceKey is required for workspace scope." });
            if (scope == BackendAuthScopes.Platform)
                workspaceKey = null;

            if (await store.ExistsByNameAsync(name, cancellationToken).ConfigureAwait(false))
                return Results.Conflict(new { error = $"An integration named '{name}' already exists." });

            var plaintextKey = IntegrationApiKey.Generate();
            var credential = new IntegrationCredential
            {
                Id = Guid.NewGuid(),
                Name = name,
                KeyHash = IntegrationApiKey.ComputeHash(plaintextKey),
                KeyPrefix = IntegrationApiKey.DerivePrefix(plaintextKey),
                RoleName = role,
                Scope = scope,
                WorkspaceKey = workspaceKey,
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = user.Identity?.Name
            };

            await store.AddAsync(credential, cancellationToken).ConfigureAwait(false);
            await auditStore.AppendAsync(
                new HostAuditEntry(
                    DateTimeOffset.UtcNow,
                    "integration.created",
                    null,
                    true,
                    user.Identity?.Name,
                    $"Integration '{name}' created with role '{role}'.",
                    new Dictionary<string, string>
                    {
                        ["integrationId"] = credential.Id.ToString(),
                        ["integrationName"] = name,
                        ["role"] = role,
                        ["scope"] = scope
                    }),
                cancellationToken).ConfigureAwait(false);

            var response = new IntegrationCreatedApiResponse(
                credential.Id, name, plaintextKey, credential.KeyPrefix, role, scope, workspaceKey);
            return Results.Created($"/api/security/integrations/{credential.Id}", response);
        })
            .WithName("Integrations_Create")
            .Produces<IntegrationCreatedApiResponse>(201)
            .RequirePermission(BackendPermissionKeys.IntegrationManage);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal user,
            [FromServices] IIntegrationCredentialStore store,
            [FromServices] IHostAuditStore auditStore,
            CancellationToken cancellationToken) =>
        {
            var revoked = await store.RevokeAsync(id, cancellationToken).ConfigureAwait(false);
            if (!revoked)
                return Results.NotFound();

            await auditStore.AppendAsync(
                new HostAuditEntry(
                    DateTimeOffset.UtcNow,
                    "integration.revoked",
                    null,
                    true,
                    user.Identity?.Name,
                    $"Integration '{id}' revoked.",
                    new Dictionary<string, string> { ["integrationId"] = id.ToString() }),
                cancellationToken).ConfigureAwait(false);

            return Results.NoContent();
        })
            .WithName("Integrations_Revoke")
            .RequirePermission(BackendPermissionKeys.IntegrationManage);
    }

    private static IntegrationApiResponse ToResponse(IntegrationCredential credential) =>
        new(
            credential.Id,
            credential.Name,
            credential.KeyPrefix,
            credential.RoleName,
            credential.Scope,
            credential.WorkspaceKey,
            credential.IsActive,
            credential.CreatedAtUtc,
            credential.RevokedAtUtc);
}
