using Callora.Core.Application.Jobs.Contracts;

namespace Callora.Core.Application.Security;

/// <summary>
/// Schedules the revocation-list purge. Hourly is enough: entries only become
/// droppable once the token they name expires, and access tokens live an hour.
/// </summary>
public sealed class SessionRevocationPurgeRecurringJobProvider : IRecurringJobProvider
{
    public IReadOnlyList<RecurringJobDefinition> GetDefinitions() =>
    [
        new RecurringJobDefinition(
            SessionRevocationPurgeJobHandler.JobTypeName,
            PayloadJson: "{}",
            Interval: TimeSpan.FromHours(1))
    ];
}
