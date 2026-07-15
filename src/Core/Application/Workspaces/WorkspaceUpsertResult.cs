namespace Callora.Core.Application.Workspaces;

public sealed record WorkspaceUpsertResult(
    WorkspaceUpsertStatus Status,
    WorkspaceSnapshot? Workspace = null);
