namespace Callora.Core.Application.Surfaces.SharedContext;

/// <summary>
/// Publishes shared context and delivers it, projected, to the connections entitled to see it.
/// <para>
/// Three gates stand between a published value and a browser, and a value has to pass all three:
/// the connection holds a matching anchor, a visible block on that surface declared it needs the
/// key, and the projection leaves something after cutting the fields the holder may not see.
/// </para>
/// </summary>
public sealed class SharedContextBroadcastService : ISharedContextService
{
    private readonly SharedContextStore _store;
    private readonly SurfaceContextBroadcaster _broadcaster;

    public SharedContextBroadcastService(
        SharedContextStore store,
        SurfaceContextBroadcaster broadcaster)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(broadcaster);
        _store = store;
        _broadcaster = broadcaster;
    }

    /// <inheritdoc />
    public bool Publish(
        SharedContextAnchor anchor,
        string key,
        IReadOnlyDictionary<string, object?>? value)
    {
        if (!_store.Publish(anchor, key, value))
        {
            return false;
        }

        _broadcaster.PublishPerConnection(connection => MessageFor(connection, key));
        return true;
    }

    /// <inheritdoc />
    public void SetParticipants(
        SharedContextAnchor anchor,
        IReadOnlyList<SharedContextParticipation> participants)
    {
        _store.SetParticipants(anchor, participants);

        // Who takes part decides who sees what, so a change to it changes what every connection
        // should be holding. Re-delivering is cheaper than reasoning about which ones moved.
        foreach (var key in KeysFor(anchor))
        {
            _broadcaster.PublishPerConnection(connection => MessageFor(connection, key));
        }
    }

    /// <inheritdoc />
    public void ReleaseConversation(SharedContextAnchor anchor)
    {
        var keys = KeysFor(anchor);
        _store.ReleaseConversation(anchor);

        // Clear what the connections still hold. A panel showing a call that ended is worse than
        // an empty one: it is wrong rather than missing.
        foreach (var key in keys)
        {
            _broadcaster.PublishPerConnection(connection => MessageFor(connection, key));
        }
    }

    /// <summary>
    /// What one connection receives for a key right now — or null for nothing, which covers "no
    /// value", "not needed here" and "not permitted" alike (§5.5 P7).
    /// </summary>
    public SurfaceContextMessage? MessageFor(SurfaceContextSubscription connection, string key)
    {
        ArgumentNullException.ThrowIfNull(connection);

        // P3: a key nobody on this surface declared does not leave the server, whatever an anchor
        // would theoretically permit. This is the gate that makes P4 — no plugin isolation in a
        // browser — survivable: what never arrives cannot be read by anyone there.
        if (!connection.RequiredKeys.Contains(key))
        {
            return null;
        }

        var value = _store.Read(connection.Anchors, key, connection.Issuer, connection.SubjectId);
        return new SurfaceContextMessage(key, value);
    }

    /// <summary>Every declared key that could hang off this anchor's type.</summary>
    private IReadOnlyList<string> KeysFor(SharedContextAnchor anchor) =>
        [.. _store.DeclaredKeys.Where(key => _store.Declaration(key)?.AnchorType == anchor.Type)];
}
