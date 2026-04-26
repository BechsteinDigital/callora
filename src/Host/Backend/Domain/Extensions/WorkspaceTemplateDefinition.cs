namespace Callora.Host.Backend.Domain.Extensions;

public sealed class WorkspaceTemplateDefinition
{
    public Guid Id { get; set; }

    public string TemplateKey { get; set; } = string.Empty;

    public string Surface { get; set; } = string.Empty;

    public string PluginId { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string TemplatePath { get; set; } = string.Empty;

    public string? ParentTemplateKey { get; set; }

    public string Scope { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int Priority { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
