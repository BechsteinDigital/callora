namespace Callora.Core.Api;

public sealed record RbacPermissionApiResponse(
    string PermissionKey,
    string Function,
    string Action);
