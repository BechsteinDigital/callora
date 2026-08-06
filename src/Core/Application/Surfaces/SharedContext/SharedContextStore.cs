using System.Collections.Concurrent;

namespace Callora.Core.Application.Surfaces.SharedContext;

/// <summary>
/// Holds shared context values against their anchors, with a time to live, and answers what a
/// given subscriber may currently see.
/// <para>
/// In-memory: shared context is live session state, not a record. It disappears with the process,
/// and that is the correct lifetime — a value describing an active call has no meaning after a
/// restart, and persisting personal data that nobody will read again would be storage without a
/// purpose.
/// </para>
/// </summary>
public sealed class SharedContextStore
{
    private readonly IReadOnlyDictionary<string, SharedContextKeyDeclaration> _declarations;
    private readonly TimeProvider _time;

    // (anchor, key) → value. Expiry is checked on read rather than swept: a value nobody asks for
    // costs a dictionary entry, and a sweeper would be a second place for the rule to live.
    private readonly ConcurrentDictionary<(string Anchor, string Key), StoredSharedContextValue> _values = new();

    // Conversation anchors only: who takes part, and how much they see. A subject anchor needs no
    // entry — it describes its own subject.
    private readonly ConcurrentDictionary<string, IReadOnlyList<SharedContextParticipation>> _participants =
        new(StringComparer.Ordinal);

    public SharedContextStore(
        IEnumerable<SharedContextKeyDeclaration> declarations,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(declarations);
        _time = timeProvider ?? TimeProvider.System;
        _declarations = declarations.ToDictionary(d => d.Key, StringComparer.Ordinal);
    }

    /// <summary>Every declared key. Names only — never values (§5.5 P6).</summary>
    public IReadOnlyCollection<string> DeclaredKeys => (IReadOnlyCollection<string>)_declarations.Keys;

    /// <summary>The declaration for a key, or null when nobody declared it.</summary>
    public SharedContextKeyDeclaration? Declaration(string key) =>
        _declarations.GetValueOrDefault(key);

    /// <summary>
    /// Records who takes part in a conversation anchor. Called by the plugin that owns the matter
    /// — it knows that the agent handling a call and the customer on it are the two sides.
    /// </summary>
    public void SetParticipants(
        SharedContextAnchor anchor,
        IReadOnlyList<SharedContextParticipation> participants)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentNullException.ThrowIfNull(participants);

        if (anchor.Type != SharedContextAnchorType.Conversation)
        {
            throw new ArgumentException(
                "Only a conversation anchor has participants; a subject anchor describes its own subject.",
                nameof(anchor));
        }

        _participants[anchor.Value] = participants;
    }

    /// <summary>Forgets a conversation and everything published under it.</summary>
    public void ReleaseConversation(SharedContextAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        _participants.TryRemove(anchor.Value, out _);
        foreach (var entry in _values.Keys.Where(k => string.Equals(k.Anchor, anchor.Value, StringComparison.Ordinal)))
        {
            _values.TryRemove(entry, out _);
        }
    }

    /// <summary>
    /// Publishes a value under an anchor. Refused — silently, returning false — when the key was
    /// never declared or the anchor type does not match the declaration: a publisher that gets
    /// this wrong should not be able to route personal data past a contract by accident.
    /// </summary>
    public bool Publish(
        SharedContextAnchor anchor,
        string key,
        IReadOnlyDictionary<string, object?>? value)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var declaration = Declaration(key);
        if (declaration is null || declaration.AnchorType != anchor.Type)
        {
            return false;
        }

        if (value is null)
        {
            _values.TryRemove((anchor.Value, key), out _);
            return true;
        }

        _values[(anchor.Value, key)] = new StoredSharedContextValue(value, _time.GetUtcNow() + declaration.TimeToLive);
        return true;
    }

    /// <summary>
    /// What a subscriber holding <paramref name="anchors"/> may see of <paramref name="key"/>,
    /// already projected — or null when there is nothing for them.
    /// <para>
    /// Null covers three different situations on purpose: no value, an expired value, and a value
    /// this subscriber may not see. A caller cannot tell them apart, which is what keeps a key
    /// they lack access to indistinguishable from one that does not exist (§5.5 P7).
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Read(
        IReadOnlyList<SharedContextAnchor> anchors,
        string key,
        string? issuer,
        string? subjectId)
    {
        ArgumentNullException.ThrowIfNull(anchors);

        var declaration = Declaration(key);
        if (declaration is null)
        {
            return null;
        }

        foreach (var anchor in anchors)
        {
            if (anchor.Type != declaration.AnchorType ||
                !_values.TryGetValue((anchor.Value, key), out var stored))
            {
                continue;
            }

            if (stored.ExpiresAtUtc <= _time.GetUtcNow())
            {
                _values.TryRemove((anchor.Value, key), out _);
                continue;
            }

            var visibility = VisibilityFor(anchor, issuer, subjectId);
            if (visibility is null)
            {
                continue;
            }

            return SharedContextProjection.Project(declaration, stored.Value, visibility.Value);
        }

        return null;
    }

    /// <summary>How much of a value the holder of this anchor sees, or null if nothing.</summary>
    private SharedContextVisibility? VisibilityFor(
        SharedContextAnchor anchor,
        string? issuer,
        string? subjectId)
    {
        // A subject anchor describes its own subject: holding it IS being the owner. The anchor
        // was derived from the session, so holding it cannot be claimed.
        if (anchor.Type == SharedContextAnchorType.Subject)
        {
            return SharedContextVisibility.Owner;
        }

        if (!_participants.TryGetValue(anchor.Value, out var participants))
        {
            // A conversation nobody assigned participants to shares nothing. Defaulting the other
            // way would make an un-configured anchor the widest one.
            return null;
        }

        foreach (var participant in participants)
        {
            if (string.Equals(participant.Issuer, issuer, StringComparison.Ordinal) &&
                string.Equals(participant.SubjectId, subjectId, StringComparison.Ordinal))
            {
                return participant.Visibility;
            }
        }

        return null;
    }
}
