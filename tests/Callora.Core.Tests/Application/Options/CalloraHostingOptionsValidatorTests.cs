using Callora.Core.Application.Options;
using Xunit;

namespace Callora.Core.Tests.Application.Options;

/// <summary>
/// Configuration that cannot produce a working host should fail where it was written, not where it
/// is used. Every value guarded here fails quietly otherwise: nobody reconnects, and it reads like a
/// bug in a plugin rather than a typo in a config file.
/// </summary>
public sealed class CalloraHostingOptionsValidatorTests
{
    [Fact]
    public void TheDefaultsAreValid()
    {
        CalloraHostingOptionsValidator.Validate(new CalloraHostingOptions());
    }

    [Fact]
    public void AZeroDrainTimeoutIsAllowedBecauseItMeansSomething()
    {
        // Documented as "skip draining entirely" — an operator has to be able to choose that.
        CalloraHostingOptionsValidator.Validate(new CalloraHostingOptions { PluginDrainTimeout = TimeSpan.Zero });
    }

    [Fact]
    public void ANegativeDrainTimeoutIsRejected()
    {
        Assert.Throws<ArgumentException>(() => CalloraHostingOptionsValidator.Validate(
            new CalloraHostingOptions { PluginDrainTimeout = TimeSpan.FromSeconds(-1) }));
    }

    [Fact]
    public void ANegativeCapabilityGracePeriodIsRejected()
    {
        Assert.Throws<ArgumentException>(() => CalloraHostingOptionsValidator.Validate(
            new CalloraHostingOptions { RuntimeCapabilityGracePeriod = TimeSpan.FromSeconds(-1) }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ANonPositiveResumeLifetimeIsRejected(int seconds)
    {
        // Zero clamps every ticket to expired-on-issue: resume would stop working with nothing
        // anywhere reporting why.
        Assert.Throws<ArgumentException>(() => CalloraHostingOptionsValidator.Validate(
            new CalloraHostingOptions { SessionResumeMaxLifetime = TimeSpan.FromSeconds(seconds) }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ANonPositivePayloadLimitIsRejected(int bytes)
    {
        // Refuses every ticket, which surfaces as "reconnect never works" rather than as a config error.
        Assert.Throws<ArgumentException>(() => CalloraHostingOptionsValidator.Validate(
            new CalloraHostingOptions { SessionResumeMaxPayloadBytes = bytes }));
    }

    [Fact]
    public void APayloadLimitPastTheHostMaximumIsRejected()
    {
        // Past this the payload stops being an identity and starts being storage the purge was not
        // sized for.
        Assert.Throws<ArgumentException>(() => CalloraHostingOptionsValidator.Validate(
            new CalloraHostingOptions
            {
                SessionResumeMaxPayloadBytes = CalloraHostingOptionsValidator.MaxSessionResumePayloadBytes + 1,
            }));
    }

    [Fact]
    public void TheHostMaximumItselfIsAccepted()
    {
        CalloraHostingOptionsValidator.Validate(new CalloraHostingOptions
        {
            SessionResumeMaxPayloadBytes = CalloraHostingOptionsValidator.MaxSessionResumePayloadBytes,
        });
    }
}
