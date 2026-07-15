namespace Callora.Core.Application.Extensions;

public sealed record WorkspaceTemplateEffectiveSnapshot(
    string TenantKey,
    string WorkspaceKey,
    string TemplateKey,
    string Surface,
    string PluginId,
    string Version,
    string DisplayName,
    string TemplatePath,
    string? ParentTemplateKey,
    string Scope,
    string Source,
    int Priority);
