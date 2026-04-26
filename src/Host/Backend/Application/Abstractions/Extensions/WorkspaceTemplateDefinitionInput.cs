namespace Callora.Host.Backend.Application.Abstractions.Extensions;

public sealed record WorkspaceTemplateDefinitionInput(
    string TemplateKey,
    string Surface,
    string DisplayName,
    string TemplatePath,
    string? ParentTemplateKey,
    string Scope,
    bool IsActive,
    int Priority);
