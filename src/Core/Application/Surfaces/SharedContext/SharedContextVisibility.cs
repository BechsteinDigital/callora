namespace Callora.Core.Application.Surfaces.SharedContext;

/// <summary>
/// How far a field of a shared context value travels. The server projects against this before
/// anything leaves it (design §5.5 P1): what arrives in a browser is already the minimum for
/// that subscriber, so there is no client-side display filter to forget — the classic GDPR
/// mistake, made impossible by construction rather than by discipline.
/// </summary>
public enum SharedContextVisibility
{
    /// <summary>
    /// Only the participant the value belongs to — the agent handling the call, the subject a
    /// subject-anchored value describes. The default for a field that declares nothing, because
    /// the safe direction to forget in is the narrow one.
    /// </summary>
    Owner = 0,

    /// <summary>
    /// Everyone the anchor connects. A customer on the same call sees a field marked this way;
    /// it is what both sides of a conversation legitimately share — call state, duration, who is
    /// speaking — not what one side happens to know about the other.
    /// </summary>
    Participant = 1,
}
