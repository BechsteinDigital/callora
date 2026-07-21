using System;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Domain.Lines;
using Xunit;

namespace Callora.Core.Tests.Communication.Domain;

public sealed class SipLineTests
{
    private static SipLine Line(bool enabled) =>
        new("line-1", "acc-1", "ws-a", "Main", "sip:alice@example.org", "+49301234567", enabled, inboundRoutingTarget: null);

    [Theory]
    // Disabled line → Disabled regardless of account/occupancy.
    [InlineData(false, SipAccountStatus.Up, false, SipLineStatus.Disabled)]
    [InlineData(false, SipAccountStatus.Failed, true, SipLineStatus.Disabled)]
    // Enabled but account not Up → Unavailable.
    [InlineData(true, SipAccountStatus.Connecting, false, SipLineStatus.Unavailable)]
    [InlineData(true, SipAccountStatus.Failed, false, SipLineStatus.Unavailable)]
    // Enabled + account Up: busy vs. available.
    [InlineData(true, SipAccountStatus.Up, true, SipLineStatus.Busy)]
    [InlineData(true, SipAccountStatus.Up, false, SipLineStatus.Available)]
    public void ResolveStatus_DerivesFromAccountEnabledAndOccupancy(
        bool enabled, SipAccountStatus accountStatus, bool isBusy, SipLineStatus expected)
    {
        Assert.Equal(expected, Line(enabled).ResolveStatus(accountStatus, isBusy));
    }

    [Fact]
    public void Ctor_EmptySipUri_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new SipLine("line-1", "acc-1", "ws-a", "Main", "  ", null, true, null));
    }
}
