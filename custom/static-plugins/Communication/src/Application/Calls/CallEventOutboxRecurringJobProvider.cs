using Callora.Core.Application.Jobs.Contracts;

namespace Callora.Plugin.Communication.Application.Calls;

/// <summary>
/// Schedules the outbox drain. Ten seconds keeps live call events close to real time for
/// consumers while still batching, and the backoff on a failing entry does the rate limiting.
/// </summary>
public sealed class CallEventOutboxRecurringJobProvider : IRecurringJobProvider
{
    /// <inheritdoc />
    public IReadOnlyList<RecurringJobDefinition> GetDefinitions() =>
    [
        new RecurringJobDefinition(
            CallEventOutboxDrainJobHandler.JobTypeName,
            PayloadJson: "{}",
            Interval: TimeSpan.FromSeconds(10))
    ];
}
