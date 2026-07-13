using Callora.Host.Backend.Application.Retention;

namespace Callora.Host.Backend.Tests.Application.Retention;

public sealed class RetentionRecurringJobProviderTests
{
    [Fact]
    public void Disabled_ReturnsNoDefinitions()
    {
        var provider = new RetentionRecurringJobProvider(new RetentionOptions { Enabled = false });

        Assert.Empty(provider.GetDefinitions());
    }

    [Fact]
    public void Enabled_SchedulesCleanupWithConfiguredInterval()
    {
        var provider = new RetentionRecurringJobProvider(new RetentionOptions
        {
            Enabled = true,
            SweepInterval = TimeSpan.FromHours(2)
        });

        var definition = Assert.Single(provider.GetDefinitions());
        Assert.Equal(RetentionCleanupJobHandler.JobTypeName, definition.JobType);
        Assert.Equal(TimeSpan.FromHours(2), definition.Interval);
    }
}
