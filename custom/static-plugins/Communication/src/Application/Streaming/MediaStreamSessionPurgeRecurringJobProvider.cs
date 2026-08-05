using Callora.Core.Application.Jobs.Contracts;

namespace Callora.Plugin.Communication.Application.Streaming;

/// <summary>
/// Schedules the media-session purge. Hourly: rows only become droppable once the
/// retention window has passed, so a tighter cadence would just re-scan.
/// </summary>
public sealed class MediaStreamSessionPurgeRecurringJobProvider : IRecurringJobProvider
{
    /// <inheritdoc />
    public IReadOnlyList<RecurringJobDefinition> GetDefinitions() =>
    [
        new RecurringJobDefinition(
            MediaStreamSessionPurgeJobHandler.JobTypeName,
            PayloadJson: "{}",
            Interval: TimeSpan.FromHours(1))
    ];
}
