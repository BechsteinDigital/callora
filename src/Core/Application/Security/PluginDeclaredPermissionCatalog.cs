using Callora.Core.Application.Persistence;
using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Plugins;

namespace Callora.Core.Application.Security;

/// <summary>
/// Collects declared permission keys by reading the manifest of every installed plugin.
/// </summary>
/// <remarks>
/// Same shape as <see cref="ContractCatalogService"/>, and for the same reason: the manifest
/// is already the source of truth, so reading it is cheaper than keeping a copy in step with
/// it. Uninstalled plugins drop out by construction — no cleanup step to forget.
/// </remarks>
public sealed class PluginDeclaredPermissionCatalog(
    IPluginInstallationRepository installations,
    IPluginPackageRegistryReader? registryReader = null) : IPluginDeclaredPermissionCatalog
{
    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (registryReader is null)
        {
            return [];
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var installation in await installations.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            if (installation.State == PluginInstallationState.Uninstalled ||
                string.IsNullOrWhiteSpace(installation.AssemblyPath))
            {
                continue;
            }

            var result = await registryReader
                .ReadForAssemblyAsync(installation.AssemblyPath, cancellationToken)
                .ConfigureAwait(false);
            foreach (var declared in result.Registry?.DeclaredPermissions ?? [])
            {
                keys.Add(declared.Key);
            }
        }

        return keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
    }
}
