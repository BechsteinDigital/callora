using System.Text.Json;

namespace Callora.Core.Api;

public sealed record UpsertFlowApiRequest(
    string Name,
    string TriggerEvent,
    JsonElement? Conditions,
    JsonElement? Actions,
    bool IsActive = true,
    int Priority = 100);
