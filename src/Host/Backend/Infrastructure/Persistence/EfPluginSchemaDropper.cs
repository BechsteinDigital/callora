using Callora.Host.Backend.Application.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Callora.Host.Backend.Infrastructure.Persistence;

/// <summary>
/// Drops a plugin schema via the host database connection (PLAT-260). The
/// schema name must already be validated to a safe identifier
/// (PluginSchemaName / PluginManifestSchemaReader) — a DDL identifier cannot
/// be a bound parameter.
/// </summary>
public sealed class EfPluginSchemaDropper(HostPersistenceDbContext dbContext) : IPluginSchemaDropper
{
    public Task DropAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
        var dropSql = "DROP SCHEMA IF EXISTS \"" + schemaName + "\" CASCADE;";
        return dbContext.Database.ExecuteSqlRawAsync(dropSql, cancellationToken);
    }
}
