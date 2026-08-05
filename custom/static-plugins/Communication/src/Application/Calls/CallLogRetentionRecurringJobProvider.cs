using Callora.Core.Application.Jobs.Contracts;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Schedules the call-history retention purge. Every six hours: the window is measured in days,
/// so a tighter cadence only rescans, and a looser one delays deletion past what an operator
/// configured.
/// </summary>
public sealed class CallLogRetentionRecurringJobProvider : IRecurringJobProvider
{
    /// <inheritdoc />
    public IReadOnlyList<RecurringJobDefinition> GetDefinitions() =>
    [
        new RecurringJobDefinition(
            CallLogRetentionJobHandler.JobTypeName,
            PayloadJson: "{}",
            Interval: TimeSpan.FromHours(6))
    ];
}
