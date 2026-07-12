namespace Callora.Host.Backend.Domain.Plugins;

/// <summary>
/// Bookkeeping row for one applied plugin migration.
/// </summary>
public sealed class PluginMigrationRecord
{
    public Guid Id { get; set; }

    public string PluginId { get; set; } = string.Empty;

    public int Version { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset AppliedAtUtc { get; set; }
}
