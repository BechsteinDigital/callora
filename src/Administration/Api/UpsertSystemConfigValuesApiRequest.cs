using System.Text.Json;

namespace Callora.Administration.Api;

/// <summary>
/// Request body for replacing configuration values of one plugin in one scope.
/// Null values delete the stored entry (falling back to the next scope).
/// </summary>
public sealed record UpsertSystemConfigValuesApiRequest(
    string PluginId,
    string Scope,
    string? ScopeKey,
    Dictionary<string, JsonElement?>? ValuesByKey);
