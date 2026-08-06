using Callora.Core.Extensibility;

namespace Callora.Core.Application.Surfaces.SharedContext;

/// <summary>
/// Publishes shared context and delivers it to the connections entitled to see it — each one
/// getting its own projection.
/// <para>
/// This is the piece that makes a value cross a surface boundary. A plugin publishes once under
/// an anchor; every browser holding that anchor receives what its holder may see, and nothing
/// else leaves the process.
/// </para>
/// </summary>
[CalloraExtensible("Extension point — resolve to publish context shared across surfaces")]
public interface ISharedContextService
{
    /// <summary>
    /// Publishes under an anchor. Returns false when the key was not declared or the anchor type
    /// does not match its declaration — a publisher cannot route personal data past a contract by
    /// getting a name wrong.
    /// </summary>
    bool Publish(SharedContextAnchor anchor, string key, IReadOnlyDictionary<string, object?>? value);

    /// <summary>Records who takes part in a conversation, and how much each of them sees.</summary>
    void SetParticipants(SharedContextAnchor anchor, IReadOnlyList<SharedContextParticipation> participants);

    /// <summary>Forgets a conversation and everything published under it.</summary>
    void ReleaseConversation(SharedContextAnchor anchor);
}
