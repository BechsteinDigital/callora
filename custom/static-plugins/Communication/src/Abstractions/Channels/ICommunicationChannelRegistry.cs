namespace Callora.Plugin.Communication.Abstractions;

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

    /// <summary>
    /// Raised when a channel is registered — consumers attach their
    /// incoming-call handling here (PLAT-257).
    /// </summary>
    event Action<string, ICommunicationChannel>? ChannelRegistered;

    /// <summary>
    /// Raised when a channel registration is removed.
    /// </summary>
    event Action<string, ICommunicationChannel>? ChannelUnregistered;

    /// <summary>
    /// Snapshot of all current registrations across workspaces — lets late
    /// consumers attach to channels registered before them.
    /// </summary>
    IReadOnlyList<(string WorkspaceKey, ICommunicationChannel Channel)> GetAllRegistrations();
}
