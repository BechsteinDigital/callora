using System.Text.Json;
using System.Text.Json.Serialization;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Abstractions;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Admin.SipAccounts;
using Callora.Plugin.Communication.Application.Voice;
using Callora.Plugin.Communication.Domain.Accounts;

namespace Callora.Plugin.Communication.Application.Admin.Numbers;

/// <summary>
/// Handles <c>POST numbers/quota</c> — says how many of a line's calls one number may hold.
/// </summary>
/// <remarks>
/// The quota lives on the account, which is where a line's capacity is divided. This route edits one
/// entry of that division rather than the whole field: setting the support number's share must not
/// quietly take away the limit somebody set on another number last month.
/// </remarks>
public sealed class SetNumberQuotaRouteHandler(
    ISipAccountStore store,
    ISipAccountRuntimeReconciler? reconciler = null,
    TimeProvider? timeProvider = null) : IHostAdminApiRouteHandler
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly SipAccountRuntimeCoordinator _runtime =
        new(store, reconciler, timeProvider ?? TimeProvider.System);

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

        SetNumberQuotaRequest? body;
        try
        {
            body = request.Body?.Deserialize<SetNumberQuotaRequest>(SerializerOptions);
        }
        catch (JsonException)
        {
            body = null;
        }

        if (body is null || string.IsNullOrWhiteSpace(body.ChannelId) || string.IsNullOrWhiteSpace(body.Number))
        {
            return Bad("channelId and number are required.");
        }

        if (body.MaxConcurrentCalls is { } requested && requested < 1)
        {
            return Bad("maxConcurrentCalls must be at least 1, or null to remove the limit.");
        }

        var account = await store.GetAsync(workspaceKey, body.ChannelId!, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return new HostAdminApiResponse(404, new { error = $"Line '{body.ChannelId}' was not found." });
        }

        var key = PhoneNumberFormat.Normalize(body.Number);
        if (key.Length == 0)
        {
            return Bad($"'{body.Number}' contains no digits to match on.");
        }

        // Everything the account divides its lines between, minus this number's old share.
        var quotas = account.CallQuotas
            .Where(quota => !PhoneNumberFormat.IsPhoneNumber(quota.Origin)
                || PhoneNumberFormat.Normalize(quota.Origin) != key)
            .ToList();

        if (body.MaxConcurrentCalls is { } limit)
        {
            quotas.Add(new CallQuota(body.Number!, limit));
        }

        account.Reconfigure(account.DisplayName, account.Connection, account.MaxConcurrentCalls, quotas);
        await store.UpdateAsync(account, cancellationToken).ConfigureAwait(false);

        // A quota is not part of the registration fingerprint, so this applies it to the live ledger
        // without dropping the calls it was raised for.
        var runtimeFailure = await _runtime.ReconcileAsync(account, cancellationToken).ConfigureAwait(false);
        return runtimeFailure ?? new HostAdminApiResponse(200, new
        {
            number = body.Number,
            channelId = account.Id,
            maxConcurrentCalls = body.MaxConcurrentCalls,
        });
    }

    private static HostAdminApiResponse Bad(string message) => new(400, new { error = message });
}
