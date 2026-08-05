using Callora.Core.Tests.Communication.Sdk;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Admin;
using Callora.Plugin.Communication.Application.Voice;
using Callora.Plugin.Communication.Domain.Accounts;
using Callora.Plugin.Communication.Infrastructure.Channels;
using Callora.Plugin.Communication.Infrastructure.Sdk;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Callora.Core.Tests.Communication.Admin;

/// <summary>
/// Connectivity has to reach the operator (#112). A registration that comes up, is lost, and
/// returns must move the persisted account status each time, and the readiness route must
/// answer from those dependencies rather than a constant.
/// </summary>
public sealed class SipAccountStatusProjectionTests
{
    [Fact]
    public async Task ChannelHealth_ProjectsOntoTheAccount_AcrossLossAndRecovery()
    {
        var channel = new FakeVoiceChannel { ChannelId = "a1", Health = ChannelHealth.Unknown };
        var (reconciler, projector) = NewReconciler(channel);
        var account = Account("a1", "w1");

        await reconciler.ApplyAsync(account);
        ReportHealth(channel, ChannelHealth.Up);
        var registered = projector.Last;

        ReportHealth(channel, ChannelHealth.Down);
        var lost = projector.Last;

        ReportHealth(channel, ChannelHealth.Up);
        var recovered = projector.Last;

        Assert.Equal(SipAccountStatus.Up, registered.Status);
        Assert.Equal(SipAccountStatus.Failed, lost.Status);
        Assert.False(string.IsNullOrWhiteSpace(lost.Error));
        Assert.Equal(SipAccountStatus.Up, recovered.Status);
    }

    [Fact]
    public async Task DegradedChannel_ProjectsDegraded_NotFailed()
    {
        var channel = new FakeVoiceChannel { ChannelId = "a1", Health = ChannelHealth.Unknown };
        var (reconciler, projector) = NewReconciler(channel);

        await reconciler.ApplyAsync(Account("a1", "w1"));
        ReportHealth(channel, ChannelHealth.Degraded);

        Assert.Equal(SipAccountStatus.Degraded, projector.Last.Status);
    }

    [Fact]
    public async Task TornDownChannel_StopsProjecting()
    {
        // A deregistered channel must not keep writing status for an account the reconciler
        // no longer owns.
        var channel = new FakeVoiceChannel { ChannelId = "a1", Health = ChannelHealth.Unknown };
        var (reconciler, projector) = NewReconciler(channel);
        await reconciler.ApplyAsync(Account("a1", "w1"));
        ReportHealth(channel, ChannelHealth.Up);

        await reconciler.RemoveAsync("w1", "a1");
        var afterTeardown = projector.Count;
        ReportHealth(channel, ChannelHealth.Down);

        Assert.Equal(afterTeardown, projector.Count);
    }

    [Fact]
    public void ReportStatus_RecordsTheLastSuccessfulRegistration()
    {
        var account = Account("a1", "w1");
        var connectedAt = new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero);

        account.ReportStatus(SipAccountStatus.Up, null, connectedAt);
        account.ReportStatus(SipAccountStatus.Failed, "registrar refused", connectedAt.AddHours(1));

        // "Never worked" and "worked until an hour ago" must be distinguishable.
        Assert.Equal(SipAccountStatus.Failed, account.Status);
        Assert.Equal(connectedAt, account.LastRegisteredAt);
        Assert.Equal(connectedAt.AddHours(1), account.LastStatusChangeAt);
        Assert.Equal("registrar refused", account.LastError);
    }

    [Fact]
    public void ReportStatus_RepeatedIdenticalReport_DoesNotMoveTheTransitionTimestamp()
    {
        var account = Account("a1", "w1");
        var first = new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero);

        account.ReportStatus(SipAccountStatus.Up, null, first);
        account.ReportStatus(SipAccountStatus.Up, null, first.AddMinutes(5));

        Assert.Equal(first, account.LastStatusChangeAt);
    }

    [Fact]
    public void ReportStatus_RedactsCredentialsOutOfProviderErrors()
    {
        var account = Account("a1", "w1");

        account.ReportStatus(
            SipAccountStatus.Failed,
            "REGISTER sip:alice:hunter2@sip.example.com failed, password=hunter2",
            DateTimeOffset.UnixEpoch);

        Assert.NotNull(account.LastError);
        Assert.DoesNotContain("hunter2", account.LastError!, StringComparison.Ordinal);
        // The useful part survives, otherwise the field would be worthless to an operator.
        Assert.Contains("sip.example.com", account.LastError!, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportStatus_BoundsTheErrorLength()
    {
        var account = Account("a1", "w1");

        account.ReportStatus(SipAccountStatus.Failed, new string('x', 5000), DateTimeOffset.UnixEpoch);

        Assert.NotNull(account.LastError);
        Assert.True(account.LastError!.Length <= SipStatusError.MaxLength + 1);
    }

    [Fact]
    public async Task Readiness_ReportsTheWorstDependency()
    {
        var registry = new CommunicationChannelRegistry();
        var channel = new FakeVoiceChannel { ChannelId = "a1", Health = ChannelHealth.Unknown };
        registry.Register("w1", channel);
        ReportHealth(channel, ChannelHealth.Up);

        var probe = new CommunicationReadinessProbe(registry);
        var ready = await probe.ProbeAsync();

        ReportHealth(channel, ChannelHealth.Down);
        var unavailable = await probe.ProbeAsync();

        Assert.Equal(CommunicationReadiness.Ready, ready.Status);
        Assert.Equal(CommunicationReadiness.Unavailable, unavailable.Status);
    }

    [Fact]
    public async Task Readiness_IgnoresDependenciesTheDeploymentDoesNotUse()
    {
        // A voice-only install without WebRTC and without persistence is ready, not degraded.
        var registry = new CommunicationChannelRegistry();
        var channel = new FakeVoiceChannel { ChannelId = "a1", Health = ChannelHealth.Unknown };
        registry.Register("w1", channel);
        ReportHealth(channel, ChannelHealth.Up);

        var status = await new CommunicationReadinessProbe(registry).ProbeAsync();

        Assert.Equal(CommunicationReadiness.Ready, status.Status);
        Assert.Contains(status.Dependencies, x => x.Name == "webrtc" && x.State == "not-configured");
        Assert.Contains(status.Dependencies, x => x.Name == "database" && x.State == "not-configured");
    }

    /// <summary>
    /// Mirrors what a provider does: the channel's health property moves, then it raises the
    /// event the reconciler listens on.
    /// </summary>
    private static void ReportHealth(FakeVoiceChannel channel, ChannelHealth health)
    {
        channel.Health = health;
        channel.RaiseHealthChanged(health);
    }

    private static (SipAccountRuntimeReconciler Reconciler, RecordingStatusProjector Projector) NewReconciler(
        IVoiceChannel channel)
    {
        var projector = new RecordingStatusProjector();
        var reconciler = new SipAccountRuntimeReconciler(
            new FakeVoiceChannelConnector().Returns("a1", channel),
            new CommunicationChannelRegistry(),
            new SdkCallAudioRegistrar(new SdkCallAudioStreamProvider(), NullLogger<SdkCallAudioRegistrar>.Instance),
            NullLogger<SipAccountRuntimeReconciler>.Instance,
            projector);
        return (reconciler, projector);
    }

    private static SipAccount Account(string id, string workspaceKey) =>
        new(
            id,
            workspaceKey,
            $"Account {id}",
            new SipConnection(
                "sip.example.com",
                5060,
                SipTransport.Udp,
                SipAccountMode.Register,
                new DigestAuthentication($"user-{id}", null, "secret-ref"),
                registrationExpirySeconds: 300),
            maxConcurrentCalls: 1,
            enabled: true);
}
