using Callora.Core.Application.Plugins;

namespace Callora.Core.Application.Security;

/// <summary>
/// Die Plugin-Berechtigungen, die in einem Workspace überhaupt etwas bedeuten.
/// </summary>
/// <remarks>
/// <para>
/// <b>Gefiltert nach Aktivierung, nicht nach Installation.</b> Ein Plugin, das auf der Anlage liegt,
/// aber in diesem Workspace nicht aktiv ist, hat hier nichts zu vergeben. Die Grenze ist der Grund,
/// warum diese Erweiterung überhaupt vertretbar ist: Ein Workspace-Admin bekommt die Rechte der
/// Plugins seines Workspace, nicht die aller Plugins der Installation.
/// </para>
/// <para>
/// <b>Der Namensraum ist die zweite Grenze</b>, und sie liegt in <see cref="IPluginPermissionMap"/>.
/// Ohne sie könnte ein Plugin über seine beigesteuerten Schlüssel etwas wie <c>user.delete</c> in eine
/// Workspace-Sitzung schreiben — eine Berechtigung, die über den Workspace hinausreicht, aus einem
/// Plugin heraus, das in ihm aktiv ist.
/// </para>
/// </remarks>
public sealed class WorkspacePluginPermissions(
    IWorkspacePluginActivationReader activations,
    IPluginPermissionMap permissions)
{
    private readonly IWorkspacePluginActivationReader _activations =
        activations ?? throw new ArgumentNullException(nameof(activations));

    private readonly IPluginPermissionMap _permissions =
        permissions ?? throw new ArgumentNullException(nameof(permissions));

    /// <summary>
    /// Was die in diesem Workspace aktiven Plugins an Berechtigungen mitbringen.
    /// </summary>
    public async Task<IReadOnlyList<string>> ForWorkspaceAsync(
        string? workspaceKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspaceKey))
        {
            return [];
        }

        var active = await _activations
            .ListActivePluginIdsAsync(workspaceKey.Trim(), cancellationToken)
            .ConfigureAwait(false);

        if (active.Count == 0)
        {
            return [];
        }

        var byPlugin = await _permissions.ByPluginAsync(cancellationToken).ConfigureAwait(false);

        return
        [
            .. active
                .Where(pluginId => byPlugin.ContainsKey(pluginId))
                .SelectMany(pluginId => byPlugin[pluginId])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
        ];
    }
}
