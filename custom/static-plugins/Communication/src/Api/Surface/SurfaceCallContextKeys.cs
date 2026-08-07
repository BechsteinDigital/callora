namespace Callora.Plugin.Communication.Api.Surface;

/// <summary>
/// The context keys Communication publishes to surfaces.
/// </summary>
/// <remarks>
/// Namespaced and versioned, like every context key: a block binds to a name that will still mean
/// the same thing after the next release, or it binds to nothing worth binding to.
/// </remarks>
public static class SurfaceCallContextKeys
{
    /// <summary>
    /// A call ringing in, waiting to be answered. Cleared the moment it is no longer waiting —
    /// answered, refused or gone.
    /// </summary>
    public const string IncomingCall = "communication.incoming-call/v1";

    /// <summary>
    /// The conversation in progress. Cleared when it ends.
    /// </summary>
    /// <remarks>
    /// One call, not a list. With several at once the most recent wins, which is the honest model
    /// while hold does not exist: a panel showing "the call you are on" cannot mean two. Something
    /// that needs every live call reads <c>calls/active</c>.
    /// </remarks>
    public const string ActiveCall = "communication.active-call/v1";
}
