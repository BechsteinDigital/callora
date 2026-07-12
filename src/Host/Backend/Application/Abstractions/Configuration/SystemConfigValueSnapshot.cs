namespace Callora.Host.Backend.Application.Abstractions.Configuration;

public sealed record SystemConfigValueSnapshot(
    string PluginId,
    string ConfigKey,
    string Scope,
    string ScopeKey,
    string ValueJson,
    DateTimeOffset UpdatedAtUtc);
