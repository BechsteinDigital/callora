namespace Callora.Plugin.Communication.Abstractions;

/// <summary>
/// Who called, and which of our numbers they reached. Present only on inbound calls.
/// </summary>
/// <remarks>
/// One record rather than five properties on <see cref="ICall"/>: it is one subject, it applies to one
/// direction, and a consumer checks a single null instead of five.
/// </remarks>
/// <param name="CalledNumber">
/// The number that was dialed — on a trunk the DID that selected the receiving line, and therefore the
/// value that says whose call this is. <see langword="null"/> when the transport does not report one.
/// </param>
/// <param name="CallerNumber">The caller's number.</param>
/// <param name="CallerDisplayName">
/// The caller's name as the network reported it. Cosmetic and unverified — a screen-pop may show it,
/// nothing should decide on it.
/// </param>
/// <param name="AssertedIdentity">
/// The caller identity a trusted peer vouches for (P-Asserted-Identity, RFC 3325). Unlike
/// <paramref name="CallerNumber"/>, which the caller controls, this comes from the network — where the
/// peer is trusted it is the identity worth routing or billing on.
/// </param>
/// <param name="DivertedFrom">
/// Where the call was diverted from (Diversion, RFC 5806) — the number that forwarded it here. Tells a
/// consumer that the caller did not dial this number directly, which changes how it should be greeted.
/// </param>
public sealed record InboundCallIdentity(
    string? CalledNumber = null,
    string? CallerNumber = null,
    string? CallerDisplayName = null,
    string? AssertedIdentity = null,
    string? DivertedFrom = null);
