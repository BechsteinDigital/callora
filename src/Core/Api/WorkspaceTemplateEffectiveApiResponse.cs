namespace Callora.Core.Api;

public sealed record WorkspaceTemplateEffectiveApiResponse(
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
