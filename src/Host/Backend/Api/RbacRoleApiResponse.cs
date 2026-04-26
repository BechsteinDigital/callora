namespace Callora.Host.Backend.Api;

public sealed record RbacRoleApiResponse(
    string Role,
    IReadOnlyList<string> Permissions);
