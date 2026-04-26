namespace Callora.Host.Backend.Application.Abstractions.Workspaces;

public sealed record WorkspacePublicUrlDescriptor(
    string? PublicBaseUrl,
    string? PublicHost,
    string PublicPathPrefix);
