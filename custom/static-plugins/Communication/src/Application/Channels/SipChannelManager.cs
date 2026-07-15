using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Accounts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Plugin.Communication.Application.Channels;

/// <summary>
/// Keeps the host channel registry in sync with the persisted SIP accounts.
/// One active account maps to one registered voice channel per workspace and
/// subscribes to inbound calls, which registers the account eagerly on sync.
/// </summary>
public sealed class SipChannelManager(
    ICommunicationChannelRegistry channelRegistry,
    IVoiceEngine engine,
    ISipAccountStore accountStore,
    ILogger<SipChannelManager>? logger = null) : IAsyncDisposable
{
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly Dictionary<string, List<IDisposable>> _registrationsByWorkspace = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger _logger = logger ?? NullLogger<SipChannelManager>.Instance;

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

        await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            RemoveWorkspaceRegistrations(workspaceKey);

            var registrations = new List<IDisposable>();
            foreach (var account in activeAccounts)
            {
                var channel = new SipCommunicationChannel(account, engine);
                registrations.Add(channelRegistry.Register(workspaceKey, channel));

                var subscription = await SubscribeIncomingCallsAsync(channel, account, cancellationToken)
                    .ConfigureAwait(false);
                if (subscription is not null)
                {
                    registrations.Add(subscription);
                }
            }

            if (registrations.Count > 0)
            {
                _registrationsByWorkspace[workspaceKey] = registrations;
            }
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _syncLock.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var workspaceKey in _registrationsByWorkspace.Keys.ToArray())
            {
                RemoveWorkspaceRegistrations(workspaceKey);
            }
        }
        finally
        {
            _syncLock.Release();
            _syncLock.Dispose();
        }
    }

    private async Task<IDisposable?> SubscribeIncomingCallsAsync(
        SipCommunicationChannel channel,
        SipAccountEntry account,
        CancellationToken cancellationToken)
    {
        try
        {
            return await engine
                .SubscribeIncomingCallsAsync(account, channel.HandleIncomingEngineCall, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Outbound stays usable over the lazily connecting engine even when
            // inbound registration fails, e.g. because the registrar is down.
            _logger.LogWarning(
                exception,
                "Inbound call subscription for SIP account '{SipAccountId}' failed; the channel stays outbound-only.",
                account.SipAccountId);
            return null;
        }
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
