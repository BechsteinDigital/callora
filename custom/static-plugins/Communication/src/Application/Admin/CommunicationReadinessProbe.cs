using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Domain.Accounts;

namespace Callora.Plugin.Communication.Application.Admin;

/// <summary>
/// Aggregates whether Communication can currently serve calls (#112).
/// <para>
/// The status route used to answer a constant <c>ok</c>, which is worse than no probe: a
/// monitor watching it stayed green through an unreachable database and a registrar outage.
/// This probe asks the dependencies that actually gate a call, and reports the worst of them.
/// </para>
/// <para>
/// A dependency the deployment does not use reports <c>not-configured</c> and never drags the
/// aggregate down. A voice-only install is ready without WebRTC, and an install without
/// persistence is ready without a database.
/// </para>
/// </summary>
public sealed class CommunicationReadinessProbe(
    ICommunicationChannelRegistry channelRegistry,
    ISipAccountStore? accountStore = null,
    bool webRtcConfigured = false)
{
    private const string Up = "up";
    private const string Degraded = "degraded";
    private const string Down = "down";
    private const string NotConfigured = "not-configured";

    private int _draining;

    /// <summary>
    /// Records that the plugin has started running its work dry (ADR-018 §2.1). From here on the
    /// aggregate reports <see cref="CommunicationReadiness.Draining"/>, which is what stops the
    /// surfaces that gate on readiness from handing out new sessions. One-way: there is no path back
    /// from draining, the plugin is stopped afterwards.
    /// </summary>
    public void MarkDraining() => Interlocked.Exchange(ref _draining, 1);

    /// <summary>Evaluates every dependency and folds them into one readiness answer.</summary>
    public async Task<CommunicationStatus> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var database = await ProbeDatabaseAsync(cancellationToken).ConfigureAwait(false);
        var channels = ProbeChannels();
        var sip = await ProbeSipAsync(cancellationToken).ConfigureAwait(false);
        var webRtc = ProbeWebRtc();

        IReadOnlyList<CommunicationDependencyStatus> dependencies = [database, channels, sip, webRtc];

        // Draining outranks the dependency fold. A quiesced line reports itself as down, and letting
        // that surface as "unavailable" would tell an operator something is broken during what is in
        // fact an orderly shutdown.
        var aggregate = Volatile.Read(ref _draining) != 0
            ? CommunicationReadiness.Draining
            : Aggregate(dependencies);

        return new CommunicationStatus(CommunicationPlugin.Id, aggregate, dependencies);
    }

    /// <summary>
    /// Reachability of the plugin's own schema. Probed with the query the account routes use, so
    /// a schema that exists but cannot be read still counts as down.
    /// </summary>
    private async Task<CommunicationDependencyStatus> ProbeDatabaseAsync(CancellationToken cancellationToken)
    {
        if (accountStore is null)
        {
            return new CommunicationDependencyStatus("database", NotConfigured, "The host provides no plugin database.");
        }

        try
        {
            _ = await accountStore.ListEnabledAsync(cancellationToken).ConfigureAwait(false);
            return new CommunicationDependencyStatus("database", Up, null);
        }
        catch (Exception ex)
        {
            return new CommunicationDependencyStatus("database", Down, SipStatusError.Redact(ex.Message));
        }
    }

    /// <summary>
    /// Whether any registered channel can carry a call. No channels at all is down, not
    /// "unknown": nothing can be dialled.
    /// </summary>
    private CommunicationDependencyStatus ProbeChannels()
    {
        var channels = channelRegistry.GetAllRegistrations();
        if (channels.Count == 0)
        {
            return new CommunicationDependencyStatus("channels", Down, "No channel is registered.");
        }

        var usable = channels.Count(x => x.Channel.Health == ChannelHealth.Up);
        if (usable == channels.Count)
        {
            return new CommunicationDependencyStatus("channels", Up, null);
        }

        return usable == 0
            ? new CommunicationDependencyStatus("channels", Down, "No registered channel is healthy.")
            : new CommunicationDependencyStatus(
                "channels",
                Degraded,
                $"{usable} of {channels.Count} registered channels are healthy.");
    }

    /// <summary>
    /// Registration state of the enabled SIP accounts, read from what the provider last
    /// reported. Without enabled accounts the deployment does not use SIP.
    /// </summary>
    private async Task<CommunicationDependencyStatus> ProbeSipAsync(CancellationToken cancellationToken)
    {
        if (accountStore is null)
        {
            return new CommunicationDependencyStatus("sip", NotConfigured, "The host provides no plugin database.");
        }

        IReadOnlyList<SipAccount> enabled;
        try
        {
            enabled = await accountStore.ListEnabledAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new CommunicationDependencyStatus("sip", Down, SipStatusError.Redact(ex.Message));
        }

        if (enabled.Count == 0)
        {
            return new CommunicationDependencyStatus("sip", NotConfigured, "No SIP account is enabled.");
        }

        var registered = enabled.Count(x => x.Status == SipAccountStatus.Up);
        if (registered == enabled.Count)
        {
            return new CommunicationDependencyStatus("sip", Up, null);
        }

        var firstError = enabled.FirstOrDefault(x => x.LastError is not null)?.LastError;
        return registered == 0
            ? new CommunicationDependencyStatus("sip", Down, firstError ?? "No enabled SIP account is registered.")
            : new CommunicationDependencyStatus(
                "sip",
                Degraded,
                firstError ?? $"{registered} of {enabled.Count} enabled SIP accounts are registered.");
    }

    /// <summary>
    /// Whether the WebRTC surface is wired. Configuration is the honest signal here: the
    /// per-session ICE and TURN reachability belongs to the session that negotiates it, not to
    /// a process-wide probe that would have to keep a candidate gathering running.
    /// </summary>
    private CommunicationDependencyStatus ProbeWebRtc() =>
        webRtcConfigured
            ? new CommunicationDependencyStatus("webrtc", Up, null)
            : new CommunicationDependencyStatus("webrtc", NotConfigured, "WebRTC is disabled for this deployment.");

    /// <summary>
    /// Worst state wins, ignoring anything not configured. Any dependency down makes the plugin
    /// unavailable, because each of them gates placing a call.
    /// </summary>
    private static string Aggregate(IReadOnlyList<CommunicationDependencyStatus> dependencies)
    {
        var relevant = dependencies.Where(x => x.State != NotConfigured).ToArray();
        if (relevant.Length == 0)
        {
            return CommunicationReadiness.Ready;
        }

        if (relevant.Any(x => x.State == Down))
        {
            return CommunicationReadiness.Unavailable;
        }

        return relevant.Any(x => x.State == Degraded)
            ? CommunicationReadiness.Degraded
            : CommunicationReadiness.Ready;
    }
}
