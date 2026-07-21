using System;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Domain.Accounts;
using Xunit;

namespace Callora.Core.Tests.Communication.Domain;

public sealed class SipAccountTests
{
    private static SipConnection ValidConnection() =>
        new("sip.example.org", 5060, SipTransport.Udp, SipAccountMode.Register, "alice", null, "secret://acc/pw", 3600);

    private static SipAccount Account(bool enabled) =>
        new("acc-1", "ws-a", "Acme Trunk", ValidConnection(), maxConcurrentCalls: 4, enabled);

    [Fact]
    public void Ctor_Enabled_StartsConnecting()
    {
        Assert.Equal(SipAccountStatus.Connecting, Account(enabled: true).Status);
    }

    [Fact]
    public void Ctor_Disabled_StartsDisabled()
    {
        Assert.Equal(SipAccountStatus.Disabled, Account(enabled: false).Status);
    }

    [Fact]
    public void Ctor_MaxConcurrentBelowOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SipAccount("acc-1", "ws-a", "Acme", ValidConnection(), maxConcurrentCalls: 0, enabled: true));
    }

    [Fact]
    public void ReportStatus_Failed_KeepsError()
    {
        var account = Account(enabled: true);

        account.ReportStatus(SipAccountStatus.Failed, "403 Forbidden", DateTimeOffset.UnixEpoch);

        Assert.Equal(SipAccountStatus.Failed, account.Status);
        Assert.Equal("403 Forbidden", account.LastError);
        Assert.Equal(DateTimeOffset.UnixEpoch, account.LastStatusChangeAt);
    }

    [Fact]
    public void ReportStatus_NonFailed_ClearsError()
    {
        var account = Account(enabled: true);
        account.ReportStatus(SipAccountStatus.Failed, "timeout", DateTimeOffset.UnixEpoch);

        account.ReportStatus(SipAccountStatus.Up, error: null, DateTimeOffset.UnixEpoch);

        Assert.Equal(SipAccountStatus.Up, account.Status);
        Assert.Null(account.LastError);
    }

    [Fact]
    public void Connection_PortOutOfRange_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SipConnection("h", 70000, SipTransport.Tls, SipAccountMode.Trunk, "u", null, "s", 60));
    }
}
