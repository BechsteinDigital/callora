namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// The issued resume promise: the secret to hand the client, and when it stops holding.
/// </summary>
/// <param name="Token">
/// The secret itself. This is the only moment it exists in readable form — the host stores a hash,
/// so a leaked database yields nothing redeemable.
/// </param>
/// <param name="ExpiresAtUtc">
/// When the promise lapses. May be earlier than the requested lifetime, since the host clamps it.
/// Send it to the client so it can stop retrying instead of reconnecting into a token the server has
/// already forgotten.
/// </param>
public sealed record HostSessionResumeTicket(string Token, DateTimeOffset ExpiresAtUtc);
