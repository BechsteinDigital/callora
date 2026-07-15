namespace Callora.Core.Api;

/// <summary>Request to create a named integration (PLAT-264).</summary>
public sealed record IntegrationCreateApiRequest(
    string? Name,
    string? Role,
    string? Scope,
    string? WorkspaceKey);
