namespace Callora.Plugin.Communication.Api.Surface;

/// <summary>
/// One call as a surface block sees it.
/// </summary>
/// <remarks>
/// Deliberately the same shape for both keys, and deliberately small. Everything published here
/// arrives in a browser tab and is readable by every script on the page, so it carries what a phone
/// panel needs to show and nothing that would only be interesting to somebody who should not have it.
/// </remarks>
/// <param name="CallId">The call, for the commands a block sends back.</param>
/// <param name="RemoteParty">Who is on the other end.</param>
/// <param name="Direction">Inbound or outbound, as a stable string.</param>
/// <param name="State">Lifecycle state, as a stable string.</param>
/// <param name="Since">When the call reached this state — a panel counts up from it.</param>
public sealed record SurfaceCallView(
    string CallId,
    string RemoteParty,
    string Direction,
    string State,
    DateTimeOffset Since);
