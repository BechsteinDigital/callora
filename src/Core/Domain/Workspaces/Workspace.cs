namespace Callora.Core.Domain.Workspaces;

public sealed class Workspace
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string WorkspaceKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string WorkspaceType { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string? PublicBaseUrl { get; set; }

    public string? PublicHost { get; set; }

    public string PublicPathPrefix { get; set; } = "/";

    public string? ThemePluginId { get; set; }

    public string? ThemeVersion { get; set; }

    public string? ThemeAssignedBy { get; set; }

    public DateTimeOffset? ThemeAssignedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Domain.Tenants.Tenant Tenant { get; set; } = null!;

    public ICollection<WorkspaceMembership> Memberships { get; set; } = new List<WorkspaceMembership>();
}
