namespace Callora.Core.Api;

public sealed record RbacRoleUpsertApiRequest(
    IReadOnlyList<RbacFunctionActionApiRequest> Functions);
