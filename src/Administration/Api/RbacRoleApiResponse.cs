namespace Callora.Administration.Api;

public sealed record RbacRoleApiResponse(
    string Role,
    IReadOnlyList<string> Permissions);
