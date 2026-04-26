namespace Callora.Host.Backend.Application.Abstractions.Workspaces;

public sealed record WorkspaceMemberUpsertResult(
    WorkspaceMemberUpsertStatus Status,
    WorkspaceMemberSnapshot? Member = null);
