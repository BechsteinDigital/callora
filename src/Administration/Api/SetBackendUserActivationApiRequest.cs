namespace Callora.Administration.Api;

/// <summary>Enables or disables an account without deleting it (#104).</summary>
/// <param name="IsActive">True re-enables the account, false deactivates it.</param>
public sealed record SetBackendUserActivationApiRequest(bool IsActive);
