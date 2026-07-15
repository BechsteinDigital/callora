namespace Callora.Core.Domain.Configuration;

/// <summary>
/// One stored configuration value for a scope. Effective resolution order is
/// workspace &gt; tenant &gt; global &gt; definition default.
/// </summary>
public sealed class SystemConfigValue
{
    public Guid Id { get; set; }

    public string PluginId { get; set; } = string.Empty;

    public string ConfigKey { get; set; } = string.Empty;

    /// <summary>Scope name: global, tenant or workspace.</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>Empty for global, tenant key or workspace key otherwise.</summary>
    public string ScopeKey { get; set; } = string.Empty;

    public string ValueJson { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
