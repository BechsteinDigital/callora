using Callora.Core.Application.Workspaces;
using Callora.Core.Extensibility;
using System.Security.Claims;

namespace Callora.Core.Application.Security;

/// <summary>
/// Answers whether a principal may act on a named workspace, across all three session scopes.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="WorkspaceScopeEvaluator.HasWorkspaceAccess"/> bleibt die reine, synchrone Antwort für
/// Betreiber und workspace-gebundene Sitzungen. Die Mandanten-Sitzung passt dort nicht hinein: Sie
/// trägt keine Workspace-Bindung, und die Frage „gehört dieser Workspace meinem Mandanten" ist eine
/// Abfrage. Deshalb dieser Dienst daneben statt einer async gewordenen statischen Methode, die jeder
/// bestehende Aufrufer mitziehen müsste.
/// </para>
/// <para>
/// Der Vergleich steht hier ausdrücklich, obwohl der Query-Filter fremde Workspaces für eine
/// Mandanten-Sitzung ohnehin verbirgt. Ein Aufruf aus einem Kontext ohne Scope — Job, Seed, ein
/// künftiger Aufrufer — bekäme den Workspace sonst zu sehen und diese Prüfung wäre eine Frage, die
/// niemand mehr stellt. Der Filter ist der Backstop, nicht die Regel.
/// </para>
/// </remarks>
[CalloraInternal("Workspace reach across session scopes — enforcement, not a plugin contract")]
public sealed class WorkspaceReach(IWorkspaceManagementStore workspaces)
{
    private readonly IWorkspaceManagementStore _workspaces =
        workspaces ?? throw new ArgumentNullException(nameof(workspaces));

    /// <summary>
    /// True when the principal may act on <paramref name="workspaceKey"/>. Fail-closed: a principal
    /// that is neither operator, nor bound to this workspace, nor a member of its tenant, is refused.
    /// </summary>
    public async Task<bool> CanReachAsync(
        ClaimsPrincipal user,
        string? workspaceKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (WorkspaceScopeEvaluator.IsOperator(user))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return false;
        }

        if (WorkspaceScopeEvaluator.HasWorkspaceAccess(user, workspaceKey))
        {
            return true;
        }

        if (!user.HasClaim(BackendClaimTypes.CalloraScope, BackendAuthScopes.Tenant))
        {
            return false;
        }

        var tenantKey = user.FindFirst(BackendClaimTypes.TenantKey)?.Value;
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            return false;
        }

        var workspace = await _workspaces
            .GetAsync(workspaceKey.Trim(), cancellationToken)
            .ConfigureAwait(false);

        return workspace is not null &&
               string.Equals(workspace.TenantKey, tenantKey.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
