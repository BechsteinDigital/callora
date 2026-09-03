namespace Callora.Core.Application.Plugins;

/// <summary>
/// Whether a tenant lets its workspace admins assign a plugin themselves.
/// </summary>
/// <remarks>
/// Fehlt ein Eintrag, lautet die Antwort <c>false</c>: Der Mandant behält die Entscheidung, bis er
/// sie abgibt. Fail-closed, weil die umgekehrte Voreinstellung bedeutete, dass jeder Workspace sich
/// nimmt, was der Mandant lizenziert hat — und der Mandant es erst merkt, wenn es passiert ist.
/// </remarks>
public interface ITenantPluginDelegationStore
{
    Task<bool> MayWorkspacesAssignAsync(
        string tenantKey,
        string pluginId,
        CancellationToken cancellationToken = default);

    /// <summary>The delegated plugin ids of the tenant — those its workspaces may assign.</summary>
    Task<IReadOnlyList<string>> ListDelegatedAsync(
        string tenantKey,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        string tenantKey,
        string pluginId,
        bool workspacesMayAssign,
        string? updatedBy,
        CancellationToken cancellationToken = default);
}
