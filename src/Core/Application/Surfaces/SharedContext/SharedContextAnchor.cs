namespace Callora.Core.Application.Surfaces.SharedContext;

/// <summary>
/// A concrete anchor: what a value hangs off, and thus who may read it.
/// <para>
/// Anchors come from the SESSION, never from the request (design §5.5 P2). There is no query
/// parameter and no header that names one — a client cannot claim an anchor it does not hold,
/// because there is no syntax in which to claim it.
/// </para>
/// </summary>
/// <param name="Type">Subject or conversation.</param>
/// <param name="Value">
/// For a subject anchor: <c>issuer|subjectId</c>, because a subject id alone is not an identity
/// (ADR-017). For a conversation anchor: the id the owning plugin minted.
/// </param>
public sealed record SharedContextAnchor(SharedContextAnchorType Type, string Value)
{
    /// <summary>The anchor a subject carries across every surface they are signed in to.</summary>
    public static SharedContextAnchor ForSubject(string issuer, string subjectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        return new SharedContextAnchor(SharedContextAnchorType.Subject, $"{issuer}|{subjectId}");
    }

    /// <summary>The anchor a plugin mints for one matter — a call, a case, a session.</summary>
    public static SharedContextAnchor ForConversation(string conversationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(conversationId);
        return new SharedContextAnchor(SharedContextAnchorType.Conversation, conversationId);
    }
}
