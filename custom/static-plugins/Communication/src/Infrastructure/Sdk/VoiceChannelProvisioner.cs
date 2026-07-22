using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Voice;
using Callora.Plugin.Communication.Domain.Accounts;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// Turns persisted, enabled <see cref="SipAccount"/>s into live, registered voice channels: each
/// account is connected through the <see cref="IVoiceChannelConnector"/> seam, wrapped in an
/// <see cref="AudioRegisteringChannel"/> so its calls feed the audio surface, and registered in the
/// workspace channel registry. One account failing to connect never blocks the others.
/// <see cref="Teardown"/> reverses it: deregister and dispose every channel it created.
/// </summary>
public sealed class VoiceChannelProvisioner
{
    private readonly IVoiceChannelConnector _connector;
    private readonly ICommunicationChannelRegistry _registry;
    private readonly SdkCallAudioRegistrar _registrar;
    private readonly ILogger<VoiceChannelProvisioner> _logger;

    private readonly List<IDisposable> _registrations = [];
    private readonly List<AudioRegisteringChannel> _channels = [];

    /// <summary>Creates a provisioner over the connector seam, channel registry and audio registrar.</summary>
    public VoiceChannelProvisioner(
        IVoiceChannelConnector connector,
        ICommunicationChannelRegistry registry,
        SdkCallAudioRegistrar registrar,
        ILogger<VoiceChannelProvisioner> logger)
    {
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(registrar);
        ArgumentNullException.ThrowIfNull(logger);

        _connector = connector;
        _registry = registry;
        _registrar = registrar;
        _logger = logger;
    }

    /// <summary>
    /// Connects and registers a channel for each account. A connect failure (null result or thrown
    /// exception) is logged and skipped so the remaining accounts still provision.
    /// </summary>
    public async Task<VoiceProvisioningSummary> ProvisionAsync(
        IReadOnlyList<SipAccount> accounts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        var connected = 0;
        foreach (var account in accounts)
        {
            var channel = await ConnectAsync(account, cancellationToken).ConfigureAwait(false);
            if (channel is null)
            {
                continue;
            }

            var decorated = new AudioRegisteringChannel(channel, _registrar);
            _registrations.Add(_registry.Register(account.WorkspaceKey, decorated));
            _channels.Add(decorated);
            connected++;
        }

        _logger.LogInformation(
            "Voice provisioning: {Connected} of {Total} enabled account(s) connected.",
            connected,
            accounts.Count);

        return new VoiceProvisioningSummary(accounts.Count, connected);
    }

    /// <summary>Deregisters and disposes every channel this provisioner created (plugin shutdown).</summary>
    public void Teardown()
    {
        foreach (var registration in _registrations)
        {
            registration.Dispose();
        }

        foreach (var channel in _channels)
        {
            channel.Dispose();
        }

        _registrations.Clear();
        _channels.Clear();
    }

    private async Task<IVoiceChannel?> ConnectAsync(SipAccount account, CancellationToken cancellationToken)
    {
        try
        {
            var channel = await _connector.ConnectAsync(account, cancellationToken).ConfigureAwait(false);
            if (channel is null)
            {
                _logger.LogWarning("Account {AccountId} did not connect; skipping.", account.Id);
            }

            return channel;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connecting account {AccountId} failed; skipping.", account.Id);
            return null;
        }
    }
}
