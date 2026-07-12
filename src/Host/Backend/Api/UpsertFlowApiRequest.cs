using System.Text.Json;

namespace Callora.Host.Backend.Api;

public sealed record UpsertFlowApiRequest(
    string Name,
    string TriggerEvent,
    JsonElement? Conditions,
    JsonElement? Actions,
    bool IsActive = true,
    int Priority = 100);
