namespace Callora.Administration.Api;

public sealed record RbacRoleUpsertApiRequest(
    IReadOnlyList<RbacFunctionActionApiRequest> Functions);
