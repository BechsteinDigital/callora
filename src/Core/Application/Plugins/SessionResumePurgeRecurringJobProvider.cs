using Callora.Core.Application.Jobs.Contracts;

namespace Callora.Core.Application.Plugins;

/// <summary>
/// Schedules the resume-promise purge. Hourly matches the granularity that matters: an expired
/// ticket is already refused on redemption, so the sweep only reclaims space.
/// </summary>
public sealed class SessionResumePurgeRecurringJobProvider : IRecurringJobProvider
{
    public IReadOnlyList<RecurringJobDefinition> GetDefinitions() =>
    [
        new RecurringJobDefinition(
            SessionResumePurgeJobHandler.JobTypeName,
            PayloadJson: "{}",
            Interval: TimeSpan.FromHours(1))
    ];
}
