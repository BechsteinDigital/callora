using Callora.Host.Backend.Domain.Security;

namespace Callora.Host.Backend.Domain.Workspaces;

public sealed class WorkspaceMembership
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    public Workspace Workspace { get; set; } = null!;

    public Guid UserId { get; set; }

    public BackendUser User { get; set; } = null!;

    public string Role { get; set; } = string.Empty;

    public DateTimeOffset AssignedAtUtc { get; set; }
}
