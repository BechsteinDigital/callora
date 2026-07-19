using Callora.Core.Application.Events.Contracts;
using Microsoft.Extensions.Logging;

namespace Callora.Administration.Api;

/// <summary>
/// Publishing helper for admin endpoints that emit business events after a mutation.
/// The publish is best-effort: the mutation has already committed, so a failed
/// post-hoc event is logged and swallowed rather than turned into an HTTP error.
/// </summary>
internal static class AdminBusinessEventPublishing
{
    public static async Task PublishSafelyAsync(
        this IBusinessEventBus businessEventBus,
        IBusinessEvent businessEvent,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            await businessEventBus.PublishAsync(businessEvent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("Callora.Administration.Api.BusinessEvents").LogWarning(
                exception, "Publishing business event {EventName} failed.", businessEvent.EventName);
        }
    }
}
