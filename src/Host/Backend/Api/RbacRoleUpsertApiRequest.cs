namespace Callora.Host.Backend.Api;

public sealed record RbacRoleUpsertApiRequest(
    IReadOnlyList<RbacFunctionActionApiRequest> Functions);
