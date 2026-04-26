namespace Callora.Host.Backend.Api;

public sealed record RbacPermissionApiResponse(
    string PermissionKey,
    string Function,
    string Action);
