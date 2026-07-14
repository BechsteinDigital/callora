namespace Callora.Host.Backend.Application.Abstractions.Persistence;

/// <summary>
/// Drops a plugin's dedicated database schema (PLAT-260). Separated from the
/// uninstall subscriber so the decision logic (which schema, when) is
/// testable without a live database.
/// </summary>
public interface IPluginSchemaDropper
{
    /// <summary>Drops the schema if it exists (idempotent).</summary>
    Task DropAsync(string schemaName, CancellationToken cancellationToken = default);
}
