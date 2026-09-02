namespace Callora.Administration.Api;

/// <summary>Welche Rollen jemand in diesem Workspace trägt.</summary>
/// <param name="UserId">Die externe Kennung des Mitglieds.</param>
/// <param name="Roles">Die zugewiesenen Rollennamen, sortiert.</param>
public sealed record WorkspaceMemberRolesApiResponse(string UserId, IReadOnlyList<string> Roles);
