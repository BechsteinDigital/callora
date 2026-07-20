namespace Callora.Administration.Api;

/// <summary>
/// Sets a workspace's surface access policy. <see cref="Policy"/> is parsed
/// case-insensitively against <c>SurfaceAccessPolicy</c> (<c>Public</c> | <c>Authenticated</c>).
/// </summary>
public sealed record SetSurfaceAccessPolicyApiRequest(string Policy);
