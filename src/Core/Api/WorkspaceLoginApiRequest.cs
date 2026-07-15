namespace Callora.Core.Api;

public sealed record WorkspaceLoginApiRequest(
    string Login,
    string Password,
    string WorkspaceKey);
