using Callora.Host.PluginContracts.Application.Migrations;

namespace Callora.Host.Backend.Application.Plugins;

/// <summary>
/// Selects pending migrations: not yet applied, ordered by version, with
/// duplicate versions rejected loudly.
/// </summary>
public static class PluginMigrationPlanner
{
    public static IReadOnlyList<IPluginMigration> SelectPending(
        IReadOnlyCollection<int> appliedVersions,
        IReadOnlyList<IPluginMigration> migrations)
    {
        ArgumentNullException.ThrowIfNull(appliedVersions);
        ArgumentNullException.ThrowIfNull(migrations);

        var duplicate = migrations
            .GroupBy(migration => migration.Version)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Plugin migration version {duplicate.Key} is declared more than once.");
        }

        return migrations
            .Where(migration => !appliedVersions.Contains(migration.Version))
            .OrderBy(migration => migration.Version)
            .ToArray();
    }
}
