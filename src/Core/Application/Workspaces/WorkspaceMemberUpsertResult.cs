namespace Callora.Core.Application.Workspaces;

public sealed record WorkspaceMemberUpsertResult(
    WorkspaceMemberUpsertStatus Status,
    WorkspaceMemberSnapshot? Member = null);
