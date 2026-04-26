namespace Callora.Host.Backend.Domain.Security;

public sealed class BackendRbacRole
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsSystem { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<BackendRbacRoleGrant> Permissions { get; set; } = new List<BackendRbacRoleGrant>();

    public ICollection<BackendRbacUserRole> UserAssignments { get; set; } = new List<BackendRbacUserRole>();
}
