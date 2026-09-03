using Callora.Core.Application.Plugins.WorkspaceAssignments;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Callora.Administration.Api.Admin.WorkspacePlugins;

/// <summary>
/// Product-level plugin assignments for one workspace. The application use case
/// keeps entitlement and workspace activation consistent; the controller owns
/// only authorization and HTTP result mapping.
/// </summary>
[ApiController]
[Authorize]
[Route("api/workspaces/{workspaceKey}/plugins")]
[Produces("application/json")]
[Tags("Workspaces")]
public sealed class WorkspacePluginsController : ControllerBase
{
    [HttpGet]
    // Derselbe Schlüssel wie die Zuweisung, und das ist enger als vorher: workspace.read hätte
    // einem Workspace-Admin die Liste ALLER Workspaces geöffnet, plugin.read das Host-Inventar
    // samt Signaturbericht. Diese Liste ist die Arbeitsfläche der Zuweisung — sie trägt deren
    // Recht und wird von derselben Reichweitenprüfung begrenzt.
    [CalloraPermission(BackendPermissionKeys.PluginAssign)]
    [ProducesResponseType<WorkspacePluginAssignmentApiResponse[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(
        string workspaceKey,
        [FromServices] WorkspacePluginAssignmentService service,
        [FromServices] WorkspaceReach reach,
        CancellationToken cancellationToken)
    {
        if (!await reach.CanReachAsync(User, workspaceKey, cancellationToken).ConfigureAwait(false))
        {
            return Forbid();
        }

        var result = await service
            .ListAsync(workspaceKey, cancellationToken)
            .ConfigureAwait(false);
        if (result.Status == WorkspacePluginAssignmentStatus.WorkspaceNotFound)
        {
            return Problem(
                detail: result.Message,
                statusCode: StatusCodes.Status404NotFound);
        }

        return Ok(result.Items.Select(ToResponse).ToArray());
    }

    [HttpPut("{pluginId}")]
    [CalloraPermission(BackendPermissionKeys.PluginAssign)]
    [ProducesResponseType<WorkspacePluginAssignmentApiResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SetAssignment(
        string workspaceKey,
        string pluginId,
        [FromBody] SetWorkspacePluginAssignmentApiRequest request,
        [FromServices] WorkspacePluginAssignmentService service,
        [FromServices] WorkspaceReach reach,
        [FromServices] PluginSelfService selfService,
        CancellationToken cancellationToken)
    {
        // Der Workspace-Schlüssel steht in der URL, also muss die Reichweite hier geprüft werden und
        // nicht erst in der Persistenz. Der Write-Backstop fängt den fremden Schreibzugriff zwar ab,
        // aber als InvalidOperationException — der Aufrufer bekäme 500 für etwas, das 403 ist, und
        // die Lesesicht wäre davon ohnehin unberührt.
        if (!await reach.CanReachAsync(User, workspaceKey, cancellationToken).ConfigureAwait(false))
        {
            return Forbid();
        }

        // Zweite, andere Frage: Reichweite heißt „darf ich diesen Workspace anfassen", hier geht es
        // um „darf ich als Workspace-Administrator dieses eine Plugin ändern". Der Mandant behält die
        // Entscheidung, bis er sie abgibt — Operatoren und Mandanten-Sitzungen meint die Regel nicht.
        if (!await selfService
                .IsAllowedAsync(User, workspaceKey, pluginId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Forbid();
        }

        var requestedBy = User.FindFirstValue("sub") ?? User.Identity?.Name;
        var result = await service
            .SetAssignedAsync(
                workspaceKey,
                pluginId,
                request.IsAssigned,
                requestedBy,
                cancellationToken)
            .ConfigureAwait(false);
        if (result.Status == WorkspacePluginAssignmentStatus.Ok &&
            result.Assignment is not null)
        {
            return Ok(ToResponse(result.Assignment));
        }

        var statusCode = result.Status switch
        {
            WorkspacePluginAssignmentStatus.WorkspaceNotFound => StatusCodes.Status404NotFound,
            WorkspacePluginAssignmentStatus.PluginNotFound => StatusCodes.Status404NotFound,
            WorkspacePluginAssignmentStatus.Forbidden => StatusCodes.Status403Forbidden,
            WorkspacePluginAssignmentStatus.PersistenceFailed => StatusCodes.Status500InternalServerError,
            _ => StatusCodes.Status400BadRequest,
        };
        return Problem(
            detail: result.Message,
            statusCode: statusCode,
            extensions: string.IsNullOrWhiteSpace(result.ErrorCode)
                ? null
                : new Dictionary<string, object?> { ["errorCode"] = result.ErrorCode });
    }

    private static WorkspacePluginAssignmentApiResponse ToResponse(
        WorkspacePluginAssignment assignment) =>
        new(
            assignment.PluginId,
            assignment.DisplayName,
            assignment.IsGloballyActive,
            assignment.IsEntitled,
            assignment.IsActive,
            assignment.IsAssigned);
}
