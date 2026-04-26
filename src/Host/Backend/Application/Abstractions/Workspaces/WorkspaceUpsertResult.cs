namespace Callora.Host.Backend.Application.Abstractions.Workspaces;

public sealed record WorkspaceUpsertResult(
    WorkspaceUpsertStatus Status,
    WorkspaceSnapshot? Workspace = null);
