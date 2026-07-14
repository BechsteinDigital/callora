namespace Callora.Host.Backend.Application.Workspaces;

public sealed record WorkspaceUpsertResult(
    WorkspaceUpsertStatus Status,
    WorkspaceSnapshot? Workspace = null);
