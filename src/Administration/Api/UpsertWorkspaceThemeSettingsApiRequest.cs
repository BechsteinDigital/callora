using System.Text.Json;

namespace Callora.Administration.Api;

public sealed record UpsertWorkspaceThemeSettingsApiRequest(
    Dictionary<string, JsonElement>? ValuesByKey);
