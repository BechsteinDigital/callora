namespace Callora.Core.Api;

public sealed record RbacRoleApiResponse(
    string Role,
    IReadOnlyList<string> Permissions);
