namespace Callora.Host.Workspace.Api;

public sealed record WorkspaceLoginApiRequest(
    string Login,
    string Password,
    string WorkspaceKey);
