namespace Callora.Core.Application.Workspaces;

public sealed record WorkspacePublicUrlDescriptor(
    string? PublicBaseUrl,
    string? PublicHost,
    string PublicPathPrefix);
