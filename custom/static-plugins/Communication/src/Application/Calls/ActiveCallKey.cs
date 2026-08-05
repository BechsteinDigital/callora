namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Identity of a tracked call (#113). A provider's call id is unique inside <em>its own</em>
/// channel, not across a deployment, so tracking by call id alone let a second channel's call
/// overwrite the first one's entry and hand its hangup to the wrong party.
/// </summary>
/// <param name="WorkspaceKey">Owning workspace.</param>
/// <param name="ChannelId">Channel the call runs on, which is also the provider scope.</param>
/// <param name="CallId">The provider's call id, unique within that channel.</param>
public readonly record struct ActiveCallKey(string WorkspaceKey, string ChannelId, string CallId)
{
    /// <summary>
    /// Case-insensitive on workspace and channel because those are operator-entered keys, and
    /// ordinal on the call id because a provider's identifier is opaque.
    /// </summary>
    public bool Equals(ActiveCallKey other) =>
        string.Equals(WorkspaceKey, other.WorkspaceKey, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(ChannelId, other.ChannelId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(CallId, other.CallId, StringComparison.Ordinal);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(
        WorkspaceKey.GetHashCode(StringComparison.OrdinalIgnoreCase),
        ChannelId.GetHashCode(StringComparison.OrdinalIgnoreCase),
        CallId.GetHashCode(StringComparison.Ordinal));
}
