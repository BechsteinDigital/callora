namespace Callora.Core.Api;

public sealed record PluginAdminNavigationApiResponse(
    string PluginId,
    string Id,
    string Label,
    string To,
    string? Icon,
    int Order);
