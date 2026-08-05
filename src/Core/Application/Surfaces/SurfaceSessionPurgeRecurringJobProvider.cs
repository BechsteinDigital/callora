using Callora.Core.Application.Jobs.Contracts;

namespace Callora.Core.Application.Surfaces;

/// <summary>
/// Schedules the surface-session purge. Hourly matches the granularity that matters:
/// an expired session is already refused on use, so the sweep only reclaims space.
/// </summary>
public sealed class SurfaceSessionPurgeRecurringJobProvider : IRecurringJobProvider
{
    public IReadOnlyList<RecurringJobDefinition> GetDefinitions() =>
    [
        new RecurringJobDefinition(
            SurfaceSessionPurgeJobHandler.JobTypeName,
            PayloadJson: "{}",
            Interval: TimeSpan.FromHours(1))
    ];
}
