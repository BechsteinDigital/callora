namespace Callora.Host.Backend.Application.Workspaces;

public sealed record WorkspacePublicUrlDescriptor(
    string? PublicBaseUrl,
    string? PublicHost,
    string PublicPathPrefix);
