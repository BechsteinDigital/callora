namespace Callora.Host.Backend.Api;

public sealed record ThemeDefinitionApiResponse(
    string TemplateKey,
    string Surface,
    string PluginId,
    string Version,
    string DisplayName,
    string TemplatePath,
    string? ParentTemplateKey,
    string Scope,
    bool IsActive,
    int Priority,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
