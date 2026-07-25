using Callora.Core.Application.Persistence.Contracts;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Reflection;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Builds and migrates one plugin's <typeparamref name="TContext"/> against
/// the host database (PLAT-260). The plugin assembly is EF's migrations
/// assembly, so plugins ship real EF migrations for their own schema; the
/// context sets its dedicated schema via HasDefaultSchema. Migration runs
/// under a Postgres advisory lock so concurrent instances cannot race.
/// </summary>
internal sealed class PluginDbContextFactory<TContext>(
    IPluginDbContextProvider provider,
    string pluginId) : IPluginDbContextFactory<TContext>
    where TContext : DbContext
{
    // The plugin's own assembly (loaded in the plugin ALC). Passed to the provider
    // as an Assembly instance so EF Core never resolves it by name from the host
    // load context, which cannot see a plugin's collectible ALC.
    private static readonly Assembly MigrationsAssembly = typeof(TContext).Assembly;

    public TContext CreateDbContext()
    {
        var builder = new DbContextOptionsBuilder<TContext>();
        provider.ConfigureOptions(builder, MigrationsAssembly);
        return (TContext)Activator.CreateInstance(typeof(TContext), builder.Options)!;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateDbContext();
        var connection = context.Database.GetDbConnection();
        var lockKey = provider.GetMigrationLockKey(pluginId);

        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ExecuteAsync(connection, $"SELECT pg_advisory_lock({lockKey});", cancellationToken).ConfigureAwait(false);
            try
            {
                await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await ExecuteAsync(connection, $"SELECT pg_advisory_unlock({lockKey});", cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
