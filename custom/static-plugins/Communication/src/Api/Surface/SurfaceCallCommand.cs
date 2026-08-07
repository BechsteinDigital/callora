namespace Callora.Plugin.Communication.Api.Surface;

/// <summary>
/// What a panel can do to one call it did not start.
/// </summary>
/// <remarks>
/// One handler for three commands rather than three near-identical ones: they differ in the verb and
/// in nothing else, and a copy each would be three places to forget the same permission check in.
/// </remarks>
public enum SurfaceCallCommand
{
    /// <summary>Answer a ringing inbound call.</summary>
    Accept = 0,

    /// <summary>Refuse a ringing inbound call.</summary>
    Reject = 1,

    /// <summary>End a call, whatever state it is in.</summary>
    Hangup = 2,
}
