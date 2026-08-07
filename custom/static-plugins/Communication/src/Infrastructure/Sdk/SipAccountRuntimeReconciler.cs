using System.Collections.Concurrent;
using System.Globalization;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Voice;
using Callora.Plugin.Communication.Domain.Accounts;
using Microsoft.Extensions.Logging;

namespace Callora.Plugin.Communication.Infrastructure.Sdk;

/// <summary>
/// The single path from a persisted <see cref="SipAccount"/> to a live, registered voice
/// channel (#110). Startup and every admin mutation call the same
/// <see cref="ApplyAsync"/>, so the runtime cannot drift from the database.
/// <para>
/// Each account is tracked with the fingerprint of the configuration it was connected under.
/// An <see cref="ApplyAsync"/> whose fingerprint is unchanged does nothing — that is what
/// makes repeated calls free. A changed fingerprint tears the old registration down before
/// connecting the new one, because a registrar generally refuses a second registration for
/// the same identity.
/// </para>
/// <para>
/// Per-account locking serializes concurrent mutations, so two operators editing the same
/// account cannot interleave into a half-provisioned state.
/// </para>
/// </summary>
public sealed class SipAccountRuntimeReconciler : ISipAccountRuntimeReconciler, IDisposable
{
    private readonly IVoiceChannelConnector _connector;
    private readonly ICommunicationChannelRegistry _registry;
    private readonly SdkCallAudioRegistrar _registrar;
    private readonly ISipAccountStatusProjector? _statusProjector;
    private readonly ICallQuotaRegistry? _quotas;
    private readonly ILogger<SipAccountRuntimeReconciler> _logger;

    private readonly ConcurrentDictionary<string, ProvisionedVoiceChannel> _provisioned = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    /// <summary>Creates a reconciler over the connector seam, channel registry and audio registrar.</summary>
    /// <param name="statusProjector">
    /// Receives the channel's health transitions so they land on the persisted account (#112).
    /// Null in a deployment without persistence, where there is no account row to update.
    /// </param>
    /// <param name="quotas">
    /// Where the account's line shares are applied. This is the only place an account becomes a live
    /// channel, so it is the only place its quotas can reach the ledger — a share nobody applies
    /// limits nothing. Null in a deployment that does not divide its lines.
    /// </param>
    public SipAccountRuntimeReconciler(
        IVoiceChannelConnector connector,
        ICommunicationChannelRegistry registry,
        SdkCallAudioRegistrar registrar,
        ILogger<SipAccountRuntimeReconciler> logger,
        ISipAccountStatusProjector? statusProjector = null,
        ICallQuotaRegistry? quotas = null)
    {
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(registrar);
        ArgumentNullException.ThrowIfNull(logger);

        _connector = connector;
        _registry = registry;
        _registrar = registrar;
        _statusProjector = statusProjector;
        _quotas = quotas;
        _logger = logger;
    }

    /// <summary>Accounts currently held as live channels — the runtime's own view.</summary>
    public int ProvisionedCount => _provisioned.Count;

    /// <inheritdoc />
    public async Task<SipRuntimeReconciliation> ApplyAsync(
        SipAccount account,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var key = KeyOf(account.WorkspaceKey, account.Id);
        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!account.Enabled)
            {
                TearDown(key);
                return SipRuntimeReconciliation.Removed;
            }

            // An account the provider cannot connect fails here rather than at the connector,
            // with the reason an operator can act on (#111). Accounts predating the edge
            // validation reach this path on startup and get that reason persisted.
            if (SipAuthMethodSupport.DescribeUnsupported(account.Connection.Authentication.Method) is { } unsupported)
            {
                TearDown(key);
                _logger.LogWarning(
                    "SIP account {AccountId} uses unsupported authentication {Method}; it stays unprovisioned.",
                    account.Id,
                    account.Connection.Authentication.Method);
                return SipRuntimeReconciliation.Failed(unsupported);
            }

            var fingerprint = Fingerprint(account);
            if (_provisioned.TryGetValue(key, out var existing))
            {
                if (string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    // Nothing changed that the registration cares about — but the line shares still
                    // reach the ledger, because that is the whole point of keeping them out of the
                    // fingerprint: raising a share must not drop the calls running under the old one.
                    ApplyQuotas(account, existing.Channel.ChannelId);
                    return SipRuntimeReconciliation.Connected;
                }

                // Credentials, endpoint or capacity changed: drop the old registration first so
                // the registrar sees one identity, then connect with the new configuration.
                TearDown(key);
            }

            IVoiceChannel? channel;
            try
            {
                channel = await _connector.ConnectAsync(account, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connecting SIP account {AccountId} failed.", account.Id);
                return SipRuntimeReconciliation.Failed("The account could not be connected.");
            }

            if (channel is null)
            {
                _logger.LogWarning("SIP account {AccountId} did not connect.", account.Id);
                return SipRuntimeReconciliation.Failed("The account could not be registered.");
            }

            var decorated = new AudioRegisteringChannel(channel, _registrar);
            var registration = _registry.Register(account.WorkspaceKey, decorated);

            // From here the channel owns the truth about connectivity, so its transitions are
            // projected onto the account instead of the account staying on "Connecting" (#112).
            var subscription = SubscribeToHealth(account, decorated);
            _provisioned[key] = new ProvisionedVoiceChannel(
                account.WorkspaceKey, registration, decorated, fingerprint, subscription);

            ApplyQuotas(account, decorated.ChannelId);

            // Record the state the channel reports right now, so a connector that returns an
            // already-registered channel does not leave the account looking like it is still coming up.
            ProjectHealth(account.WorkspaceKey, account.Id, decorated.Health);
            return SipRuntimeReconciliation.Connected;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<SipRuntimeReconciliation> RemoveAsync(
        string workspaceKey,
        string accountId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);

        var key = KeyOf(workspaceKey, accountId);
        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            TearDown(key);
            return SipRuntimeReconciliation.Removed;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Applies every account in one pass and reports how many ended up live. One account
    /// failing never blocks the others — a single unreachable registrar must not take the
    /// whole deployment's voice surface down.
    /// </summary>
    public async Task<VoiceProvisioningSummary> ApplyAllAsync(
        IReadOnlyList<SipAccount> accounts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        var connected = 0;
        foreach (var account in accounts)
        {
            var result = await ApplyAsync(account, cancellationToken).ConfigureAwait(false);
            if (result.State == SipRuntimeState.Connected)
            {
                connected++;
            }
        }

        _logger.LogInformation(
            "Voice provisioning: {Connected} of {Total} enabled account(s) connected.",
            connected,
            accounts.Count);

        return new VoiceProvisioningSummary(accounts.Count, connected);
    }

    /// <summary>Deregisters and disposes every channel this reconciler created (plugin shutdown).</summary>
    public void Teardown()
    {
        foreach (var key in _provisioned.Keys.ToArray())
        {
            TearDown(key);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Teardown();
        foreach (var gate in _locks.Values)
        {
            gate.Dispose();
        }

        _locks.Clear();
    }

    private void TearDown(string key)
    {
        if (!_provisioned.TryRemove(key, out var provisioned))
        {
            return;
        }

        // Lines that no longer exist cannot be divided, and a share left behind would apply again the
        // moment an account with the same channel id came back.
        _quotas?.Configure(provisioned.WorkspaceKey, provisioned.Channel.ChannelId, new Dictionary<string, int>());

        provisioned.HealthSubscription?.Dispose();
        provisioned.Registration.Dispose();
        provisioned.Channel.Dispose();
    }

    /// <summary>
    /// Hands the account's line shares to the ledger, replacing whatever was there. Called on every
    /// apply, including the one that changes nothing else — a quota is deliberately not part of the
    /// fingerprint, because re-registering to raise a share would drop every call running under the
    /// old one.
    /// </summary>
    private void ApplyQuotas(SipAccount account, string channelId) =>
        _quotas?.Configure(
            account.WorkspaceKey,
            channelId,
            account.CallQuotas.ToDictionary(q => q.Origin, q => q.MaxConcurrentCalls, StringComparer.Ordinal));

    /// <summary>
    /// Bridges the channel's health events onto the account, returning a handle that detaches
    /// the handler. Without detaching, a torn-down channel would keep writing status for an
    /// account the reconciler no longer owns.
    /// </summary>
    private IDisposable? SubscribeToHealth(SipAccount account, ICommunicationChannel channel)
    {
        if (_statusProjector is null)
        {
            return null;
        }

        var workspaceKey = account.WorkspaceKey;
        var accountId = account.Id;

        void OnHealthChanged(object? sender, ChannelHealthChangedEventArgs args) =>
            ProjectHealth(workspaceKey, accountId, args.Health);

        channel.HealthChanged += OnHealthChanged;
        return new HealthSubscription(() => channel.HealthChanged -= OnHealthChanged);
    }

    /// <summary>
    /// Fires the projection without awaiting it. The caller is a provider callback that must not
    /// block on a database write; the projector itself never throws.
    /// </summary>
    private void ProjectHealth(string workspaceKey, string accountId, ChannelHealth health)
    {
        if (_statusProjector is null)
        {
            return;
        }

        var status = MapHealth(health);
        var error = health == ChannelHealth.Down ? "The channel reported no usable registration." : null;
        _ = _statusProjector.ProjectAsync(workspaceKey, accountId, status, error, CancellationToken.None);
    }

    /// <summary>
    /// Channel health to account status. <see cref="ChannelHealth.Unknown"/> maps to
    /// <see cref="SipAccountStatus.Connecting"/> rather than a failure: the channel exists and
    /// has simply not reported yet.
    /// </summary>
    private static SipAccountStatus MapHealth(ChannelHealth health) => health switch
    {
        ChannelHealth.Up => SipAccountStatus.Up,
        ChannelHealth.Degraded => SipAccountStatus.Degraded,
        ChannelHealth.Down => SipAccountStatus.Failed,
        _ => SipAccountStatus.Connecting
    };

    private static string KeyOf(string workspaceKey, string accountId) =>
        string.Concat(workspaceKey.Trim(), "/", accountId.Trim());

    /// <summary>
    /// Everything a live registration depends on. Comparing it is how the reconciler decides
    /// between "already correct" and "must reconnect"; the display name is deliberately absent
    /// because renaming an account is not a runtime change.
    /// </summary>
    private static string Fingerprint(SipAccount account)
    {
        var connection = account.Connection;
        return string.Join(
            '|',
            connection.Host,
            connection.Port.ToString(CultureInfo.InvariantCulture),
            connection.Transport.ToString(),
            connection.Mode.ToString(),
            connection.RegistrationExpirySeconds?.ToString(CultureInfo.InvariantCulture) ?? "-",
            connection.OutboundProxy ?? "-",
            string.Join(',', connection.InboundNumbers),
            FingerprintAuthentication(connection.Authentication),
            account.MaxConcurrentCalls.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Identity part of the fingerprint. Secret <em>references</em> are compared, never secret
    /// values: rotating the stored password behind an unchanged reference is a credential change
    /// the reconciler cannot see, so callers rotate the reference alongside it.
    /// </summary>
    private static string FingerprintAuthentication(SipAuthentication authentication) => authentication switch
    {
        DigestAuthentication digest =>
            $"digest:{digest.Username}:{digest.AuthId ?? "-"}:{digest.PasswordSecretRef}",
        MutualTlsAuthentication mutualTls =>
            $"mtls:{mutualTls.ClientCertificateSecretRef}",
        IpAuthentication => "ip",
        _ => authentication.Method.ToString()
    };

}
