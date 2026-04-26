namespace Callora.Host.Backend.Api;

public sealed record ThemeDefinitionUpsertApiRequest(
    string DisplayName,
    string TemplatePath,
    string? ParentTemplateKey,
    string Scope,
    bool IsActive,
    int Priority,
    string Surface = "workspace");
