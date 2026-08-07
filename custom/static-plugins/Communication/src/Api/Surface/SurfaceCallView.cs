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
/// <param name="CallerName">
/// The caller's name as the network reported it. Cosmetic and unverified — it belongs on a screen,
/// never in a decision.
/// </param>
/// <param name="CalledNumber">
/// Which of our numbers they reached. On a shared trunk that is what tells an agent which service
/// is being called before they say a word.
/// </param>
/// <param name="DivertedFrom">
/// The number that forwarded the call here, when one did. It changes how a call is answered, which
/// is why it travels and the raw asserted identity does not.
/// </param>
/// <param name="Verified">
/// Whether a trusted peer vouched for the caller (P-Asserted-Identity). A flag rather than the
/// second number: an agent needs to know how much to trust what they see, not to compare two
/// strings of digits.
/// </param>
public sealed record SurfaceCallView(
    string CallId,
    string RemoteParty,
    string Direction,
    string State,
    DateTimeOffset Since,
    string? CallerName = null,
    string? CalledNumber = null,
    string? DivertedFrom = null,
    bool Verified = false);
