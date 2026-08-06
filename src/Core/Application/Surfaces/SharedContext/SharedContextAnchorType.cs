namespace Callora.Core.Application.Surfaces.SharedContext;

/// <summary>
/// What a shared context value hangs off. Never the workspace — every portal visitor would
/// then see what an agent is doing (design §5.2).
/// </summary>
public enum SharedContextAnchorType
{
    /// <summary>
    /// The same actor across several surfaces. An agent desk and a video surface share the
    /// anchor because they carry the same <c>SurfaceSubject</c> identity (issuer + subjectId).
    /// The value is the subject's own, so they see all of it.
    /// </summary>
    Subject = 0,

    /// <summary>
    /// The same matter across different people. An agent and a customer hang off the same call
    /// although they are different persons on different surfaces. A plugin creates the anchor
    /// and assigns participants; what each of them sees follows from their role.
    /// </summary>
    Conversation = 1,
}
