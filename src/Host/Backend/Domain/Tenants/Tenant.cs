namespace Callora.Host.Backend.Domain.Tenants;

public sealed class Tenant
{
    public Guid Id { get; set; }

    public string TenantKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<Workspaces.Workspace> Workspaces { get; set; } = new List<Workspaces.Workspace>();
}
