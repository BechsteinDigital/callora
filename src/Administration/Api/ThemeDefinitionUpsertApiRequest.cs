namespace Callora.Administration.Api;

public sealed record ThemeDefinitionUpsertApiRequest(
    string DisplayName,
    string TemplatePath,
    string? ParentTemplateKey,
    string Scope,
    bool IsActive,
    int Priority,
    string Surface = "surface");
