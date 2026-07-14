namespace Callora.Host.Backend.Application.Workspaces;

public sealed record WorkspaceMemberUpsertResult(
    WorkspaceMemberUpsertStatus Status,
    WorkspaceMemberSnapshot? Member = null);
