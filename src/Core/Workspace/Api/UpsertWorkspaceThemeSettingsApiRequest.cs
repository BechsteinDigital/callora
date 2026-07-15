using System.Text.Json;

namespace Callora.Host.Workspace.Api;

public sealed record UpsertWorkspaceThemeSettingsApiRequest(
    Dictionary<string, JsonElement>? ValuesByKey);
