using Callora.Contracts.Communication;
using Callora.Plugins.Voip.Application.Accounts;

namespace Callora.Plugins.Voip.Application.Channels;

/// <summary>
/// Keeps the host channel registry in sync with the persisted SIP accounts.
/// One active account maps to one registered voice channel per workspace.
/// </summary>
public sealed class SipChannelManager(
    ICommunicationChannelRegistry channelRegistry,
    IVoiceEngine engine,
    ISipAccountStore accountStore) : IAsyncDisposable
{
    private readonly object _syncLock = new();
    private readonly Dictionary<string, List<IDisposable>> _registrationsByWorkspace = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Rebuilds channel registrations for all workspaces with persisted accounts.
    /// </summary>
    public async Task SynchronizeAllAsync(CancellationToken cancellationToken = default)
    {
        var workspaceKeys = await accountStore.ListWorkspaceKeysAsync(cancellationToken).ConfigureAwait(false);
        foreach (var workspaceKey in workspaceKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SynchronizeWorkspaceAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Rebuilds channel registrations for one workspace from its persisted accounts.
    /// </summary>
    public async Task SynchronizeWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);

        var accounts = await accountStore.ListAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        var activeAccounts = accounts.Where(static account => account.IsActive).ToArray();

        lock (_syncLock)
        {
            RemoveWorkspaceRegistrations(workspaceKey);

            var registrations = new List<IDisposable>(activeAccounts.Length);
            foreach (var account in activeAccounts)
            {
                var channel = new SipCommunicationChannel(account, engine);
                registrations.Add(channelRegistry.Register(workspaceKey, channel));
            }

            if (registrations.Count > 0)
            {
                _registrationsByWorkspace[workspaceKey] = registrations;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_syncLock)
        {
            foreach (var workspaceKey in _registrationsByWorkspace.Keys.ToArray())
            {
                RemoveWorkspaceRegistrations(workspaceKey);
            }
        }

        return ValueTask.CompletedTask;
    }

    private void RemoveWorkspaceRegistrations(string workspaceKey)
    {
        if (!_registrationsByWorkspace.Remove(workspaceKey, out var registrations))
            return;

        foreach (var registration in registrations)
        {
            registration.Dispose();
        }
    }
}
