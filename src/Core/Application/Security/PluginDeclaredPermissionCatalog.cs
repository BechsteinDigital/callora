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
        var byPlugin = await ListByPluginAsync(cancellationToken).ConfigureAwait(false);

        return byPlugin.Values
            .SelectMany(keys => keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ListByPluginAsync(
        CancellationToken cancellationToken = default)
    {
        if (registryReader is null)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        }

        var byPlugin = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
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

            var keys = (result.Registry?.DeclaredPermissions ?? [])
                .Select(declared => declared.Key)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();

            if (keys.Length > 0)
            {
                // Der Schlüssel ist die installierte PluginId, nicht die aus dem Manifest: Was der Host
                // unter diesem Plugin führt, entscheidet die Installation, und ein Manifest, das sich
                // anders nennt, würde hier eine zweite Zeile für dasselbe Plugin erzeugen.
                byPlugin[installation.PluginId] = keys;
            }
        }

        return byPlugin;
    }
}
