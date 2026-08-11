using Callora.Core.Application.Audit;
using Callora.Core.Application.Integrations;
using Callora.Core.Application.Security;
using Callora.Core.Domain.Integrations;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Callora.Administration.Api.Admin.Integrations;

/// <summary>
/// Management API for named machine-to-machine integrations (PLAT-264). Each
/// integration carries its own RBAC role and scope instead of the global,
/// super-admin bootstrap key; creation and revocation are audited.
/// </summary>
[ApiController]
[Authorize]
[Route("api/security/integrations")]
[Produces("application/json")]
[Tags("Security")]
public sealed class IntegrationsController : ControllerBase
{
    [HttpGet]
    [CalloraPermission(BackendPermissionKeys.IntegrationRead)]
    [ProducesResponseType<IntegrationApiResponse[]>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromServices] IIntegrationCredentialStore store,
        CancellationToken cancellationToken)
    {
        var items = (await store.ListAsync(cancellationToken).ConfigureAwait(false))
            .Select(ToResponse)
            .ToArray();
        return Ok(items);
    }

    [HttpPost]
    [CalloraPermission(BackendPermissionKeys.IntegrationManage)]
    [ProducesResponseType<IntegrationCreatedApiResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] IntegrationCreateApiRequest request,
        [FromServices] IIntegrationCredentialStore store,
        [FromServices] IBackendRbacStore rbacStore,
        [FromServices] IHostAuditStore auditStore,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { error = "name is required." });
        }

        var role = request.Role?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(role))
        {
            return BadRequest(new { error = "role is required." });
        }

        // Operator roles would defeat the bounded-access purpose of an integration.
        if (string.Equals(role, BackendRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(role, BackendRoles.HostApi, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Integrations may not use operator roles." });
        }

        var knownPermissions = await rbacStore.GetRolePermissionsAsync(role, cancellationToken).ConfigureAwait(false);
        if (knownPermissions is null)
        {
            return BadRequest(new { error = $"Unknown RBAC role '{role}'." });
        }

        // Pflichtfeld, kein Default. Ein fehlendes scope hieß vorher "platform" — die
        // weitreichendere der beiden Möglichkeiten, ausgewählt durch Weglassen. Die Autorität
        // begrenzt zwar weiterhin die geprüfte RBAC-Rolle, aber die Reichweite eines Schlüssels
        // gehört ausgeschrieben und nicht geraten.
        var scope = request.Scope?.Trim().ToLowerInvariant();
        if (scope != BackendAuthScopes.Platform && scope != BackendAuthScopes.Workspace)
        {
            return BadRequest(new { error = "scope is required and must be 'platform' or 'workspace'." });
        }

        var workspaceKey = request.WorkspaceKey?.Trim();
        if (scope == BackendAuthScopes.Workspace && string.IsNullOrWhiteSpace(workspaceKey))
        {
            return BadRequest(new { error = "workspaceKey is required for workspace scope." });
        }

        if (scope == BackendAuthScopes.Platform)
        {
            workspaceKey = null;
        }

        if (await store.ExistsByNameAsync(name, cancellationToken).ConfigureAwait(false))
        {
            return Conflict(new { error = $"An integration named '{name}' already exists." });
        }

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
            CreatedBy = User.Identity?.Name
        };

        await store.AddAsync(credential, cancellationToken).ConfigureAwait(false);
        await auditStore.AppendAsync(
            new HostAuditEntry(
                DateTimeOffset.UtcNow,
                "integration.created",
                null,
                true,
                User.Identity?.Name,
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
        return Created($"/api/security/integrations/{credential.Id}", response);
    }

    [HttpDelete("{id:guid}")]
    [CalloraPermission(BackendPermissionKeys.IntegrationManage)]
    public async Task<IActionResult> Revoke(
        Guid id,
        [FromServices] IIntegrationCredentialStore store,
        [FromServices] IHostAuditStore auditStore,
        CancellationToken cancellationToken)
    {
        var revoked = await store.RevokeAsync(id, cancellationToken).ConfigureAwait(false);
        if (!revoked)
        {
            return NotFound();
        }

        await auditStore.AppendAsync(
            new HostAuditEntry(
                DateTimeOffset.UtcNow,
                "integration.revoked",
                null,
                true,
                User.Identity?.Name,
                $"Integration '{id}' revoked.",
                new Dictionary<string, string> { ["integrationId"] = id.ToString() }),
            cancellationToken).ConfigureAwait(false);

        return NoContent();
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
