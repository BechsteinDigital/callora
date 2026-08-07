namespace Callora.Plugin.Communication.Api.Surface;

/// <summary>
/// One of the workspace's lines, as somebody working at a phone needs to see it.
/// </summary>
/// <remarks>
/// Deliberately not the operator's view of an account: no host, no port, no transport, no
/// credentials. What an agent asks is "why is nothing ringing", and the answer to that is a state
/// and a moment — everything else would be information they cannot act on anyway.
/// </remarks>
/// <param name="ChannelId">The line, for correlating with a call.</param>
/// <param name="DisplayName">What the operator named it.</param>
/// <param name="Status">Connectivity as a stable string: <c>Up</c>, <c>Degraded</c>, <c>Failed</c>, …</param>
/// <param name="Since">
/// When it last changed to that state. "Getrennt" is worrying; "getrennt seit zwei Minuten" is
/// actionable, and "seit gestern" is a different conversation.
/// </param>
/// <param name="LastRegisteredAt">
/// When the line last worked. Survives a later failure, so "never worked" and "worked until an hour
/// ago" stay distinguishable — the two need different people to fix them.
/// </param>
/// <param name="Error">
/// Why it is not up, when the provider said. Already redacted in the domain: a provider message can
/// carry the credential that caused the failure.
/// </param>
public sealed record SurfaceChannelView(
    string ChannelId,
    string DisplayName,
    string Status,
    DateTimeOffset? Since,
    DateTimeOffset? LastRegisteredAt,
    string? Error);
