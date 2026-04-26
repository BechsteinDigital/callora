namespace Callora.Host.Backend.Application.Abstractions.Extensions;

public sealed record WorkspaceTemplateDefinitionSnapshot(
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
