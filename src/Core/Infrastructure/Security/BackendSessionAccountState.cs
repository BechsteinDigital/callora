namespace Callora.Core.Infrastructure.Security;

/// <summary>Revocation-relevant state of one account.</summary>
/// <param name="SecurityStamp">The account's current stamp.</param>
/// <param name="IsDisabled">Whether the account is deactivated.</param>
public sealed record BackendSessionAccountState(string SecurityStamp, bool IsDisabled);
