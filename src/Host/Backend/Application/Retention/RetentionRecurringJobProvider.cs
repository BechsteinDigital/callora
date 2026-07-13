using Callora.Host.PluginContracts.Application.Jobs;

namespace Callora.Host.Backend.Application.Retention;

/// <summary>
/// Schedules the retention cleanup as a fixed-interval recurring job.
/// </summary>
public sealed class RetentionRecurringJobProvider(RetentionOptions options) : IRecurringJobProvider
{
    public IReadOnlyList<RecurringJobDefinition> GetDefinitions()
    {
        if (!options.Enabled)
        {
            return [];
        }

        return
        [
            new RecurringJobDefinition(
                RetentionCleanupJobHandler.JobTypeName,
                PayloadJson: "{}",
                Interval: options.SweepInterval)
        ];
    }
}
