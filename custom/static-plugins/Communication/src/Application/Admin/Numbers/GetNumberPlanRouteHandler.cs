using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Admin.SipAccounts;

namespace Callora.Plugin.Communication.Application.Admin.Numbers;

/// <summary>
/// Handles <c>GET numbers</c> — the workspace's number plan: which line delivers each number, how
/// much of that line it may hold, and what has been arriving on it.
/// </summary>
public sealed class GetNumberPlanRouteHandler(
    IInboundNumberCatalog catalog,
    ISipAccountStore accounts,
    ICallHistory history) : IHostAdminApiRouteHandler
{
    /// <summary>
    /// How far back the activity column looks. Enough to answer "did anything arrive here", not a
    /// report — a number plan is a configuration screen, and a full scan would make opening it slow
    /// for the one deployment that needs it most.
    /// </summary>
    private const int ActivityWindow = 200;

    /// <inheritdoc />
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!SipAccountAdminScope.TryResolve(request, out var workspaceKey, out var scopeError))
        {
            return scopeError!;
        }

        var numbers = await catalog.ListAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        var lines = await accounts.ListAsync(workspaceKey, cancellationToken).ConfigureAwait(false);
        var recent = await history.ListRecentAsync(workspaceKey, ActivityWindow, cancellationToken).ConfigureAwait(false);

        // Keyed by the one rule for whether two written numbers are the same line: the operator typed
        // the quota, the account holds it, and the trunk reported the call — three spellings, one line.
        var quotas = lines
            .SelectMany(line => line.CallQuotas.Select(quota => (line.Id, quota)))
            .Where(entry => PhoneNumberFormat.IsPhoneNumber(entry.quota.Origin))
            .ToDictionary(
                entry => (entry.Id, PhoneNumberFormat.Normalize(entry.quota.Origin)),
                entry => entry.quota.MaxConcurrentCalls);

        var arrivals = recent
            .Select(call => (Key: PhoneNumberFormat.Normalize(call.LocalIdentity), call.StartedAt))
            .Where(call => call.Key.Length > 0)
            .GroupBy(call => call.Key)
            .ToDictionary(group => group.Key, group => (Count: group.Count(), Last: group.Max(c => c.StartedAt)));

        NumberPlanEntry[] plan =
        [
            .. numbers.Select(number =>
            {
                var key = PhoneNumberFormat.Normalize(number.Number);
                var activity = arrivals.TryGetValue(key, out var seen) ? seen : (Count: 0, Last: (DateTimeOffset?)null);

                return new NumberPlanEntry(
                    number.Number,
                    number.ChannelId,
                    number.ChannelDisplayName,
                    quotas.TryGetValue((number.ChannelId, key), out var limit) ? limit : null,
                    activity.Count,
                    activity.Last);
            })
        ];

        return new HostAdminApiResponse(200, plan);
    }
}
