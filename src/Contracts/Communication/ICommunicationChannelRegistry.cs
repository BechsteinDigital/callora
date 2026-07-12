namespace Callora.Contracts.Communication;

/// <summary>
/// Workspace-scoped registry of communication channels. The host provides the
/// implementation; communication plugins register their channels, consuming
/// plugins resolve them without knowing the providing plugin.
/// </summary>
public interface ICommunicationChannelRegistry
{
    /// <summary>
    /// Registers one channel for one workspace. Disposing the returned handle
    /// removes the registration (typically on plugin deactivation).
    /// </summary>
    IDisposable Register(string workspaceKey, ICommunicationChannel channel);

    /// <summary>
    /// Returns all channels registered for one workspace.
    /// </summary>
    IReadOnlyList<ICommunicationChannel> GetChannels(string workspaceKey);

    /// <summary>
    /// Returns all channels for one workspace that provide the given capability code.
    /// </summary>
    IReadOnlyList<ICommunicationChannel> GetChannelsByCapability(string workspaceKey, string capability);

    /// <summary>
    /// Tries to resolve one channel by id within one workspace.
    /// </summary>
    bool TryGetChannel(string workspaceKey, string channelId, out ICommunicationChannel? channel);
}
