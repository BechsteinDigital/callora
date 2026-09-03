using Callora.Core.Application.Plugins;
using Callora.Core.Application.Workspaces;
using Callora.Core.Extensibility;
using System.Security.Claims;

namespace Callora.Core.Application.Security;

/// <summary>
/// Whether a workspace administrator may assign this plugin to their own workspace, or whether the
/// tenant has kept that decision.
/// </summary>
/// <remarks>
/// <para>
/// Getrennt von <see cref="WorkspaceReach"/>, weil es eine andere Frage ist: Reichweite heißt „darf
/// ich diesen Workspace überhaupt anfassen", Selbstbedienung heißt „darf ich als
/// Workspace-Administrator dieses eine Plugin ändern". Beide zusammen in einem Dienst hieße, dass
/// eine Änderung an der einen Regel die andere mitbewegt, ohne dass es jemandem auffällt.
/// </para>
/// <para>
/// Für Operatoren und Mandanten-Sitzungen ist die Antwort immer ja — nicht weil sie das Recht
/// überschreiben, sondern weil die Regel sie nicht meint: Die Delegation ist die Entscheidung des
/// Mandanten über SEINE Workspaces, und wer sie trifft, unterliegt ihr nicht.
/// </para>
/// </remarks>
[CalloraInternal("Plugin self-service gate — enforcement, not a plugin contract")]
public sealed class PluginSelfService(
    IWorkspaceManagementStore workspaces,
    ITenantPluginDelegationStore delegations)
{
    private readonly IWorkspaceManagementStore _workspaces =
        workspaces ?? throw new ArgumentNullException(nameof(workspaces));

    private readonly ITenantPluginDelegationStore _delegations =
        delegations ?? throw new ArgumentNullException(nameof(delegations));

    public async Task<bool> IsAllowedAsync(
        ClaimsPrincipal user,
        string? workspaceKey,
        string? pluginId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!user.HasClaim(BackendClaimTypes.CalloraScope, BackendAuthScopes.Workspace))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(workspaceKey) || string.IsNullOrWhiteSpace(pluginId))
        {
            return false;
        }

        var workspace = await _workspaces
            .GetAsync(workspaceKey.Trim(), cancellationToken)
            .ConfigureAwait(false);
        if (workspace is null)
        {
            return false;
        }

        return await _delegations
            .MayWorkspacesAssignAsync(workspace.TenantKey, pluginId.Trim(), cancellationToken)
            .ConfigureAwait(false);
    }
}
