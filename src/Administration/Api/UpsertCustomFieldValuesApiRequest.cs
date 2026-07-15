using System.Text.Json;

namespace Callora.Administration.Api;

/// <summary>
/// Request body for replacing custom field values on one entity instance.
/// Null values delete the stored entry.
/// </summary>
public sealed record UpsertCustomFieldValuesApiRequest(Dictionary<string, JsonElement?>? ValuesByKey);
