using Callora.Core.Domain.Workspaces;

namespace Callora.Core.Domain.Security;

public sealed class BackendUser
{
    public Guid Id { get; set; }

    public string ExternalId { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? PasswordHash { get; set; }

    public string? PasswordHashAlgorithm { get; set; }

    public string? DisplayName { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<BackendRbacUserRole> RoleAssignments { get; set; } = new List<BackendRbacUserRole>();

    public ICollection<WorkspaceMembership> WorkspaceMemberships { get; set; } = new List<WorkspaceMembership>();
}
