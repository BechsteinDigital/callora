using System.Text.Json;
using System.Text.Json.Serialization;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Secrets.Contracts;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Voice;

namespace Callora.Plugin.Communication.Application.Admin.SipAccounts;

/// <summary>
/// Handles <c>PUT sip-accounts/{accountId}</c> — replaces an account's editable configuration in the
/// caller's workspace (display name, connection incl. authentication method, max concurrent calls).
/// Omitted secret material keeps the stored credential; omitted max-concurrent-calls keeps the current
/// value. The enabled/status lifecycle is untouched (that is the enable/disable routes). 404 when the
/// account is not in the caller's workspace.
/// </summary>
public sealed class UpdateSipAccountRouteHandler(
    ISipAccountStore store,
    IPluginDataProtector dataProtector,
    string pluginId,
    ISipAccountRuntimeReconciler? reconciler = null,
    TimeProvider? timeProvider = null) : IHostAdminApiRouteHandler
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly SipAccountConnectionFactory _connectionFactory = new(dataProtector, pluginId);
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

        var accountId = request.RouteValues.TryGetValue("accountId", out var value) ? value : string.Empty;
        var account = await store.GetAsync(workspaceKey, accountId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return new HostAdminApiResponse(404, new { error = $"SIP account '{accountId}' was not found." });
        }

        UpdateSipAccountRequest? body;
        try
        {
            body = request.Body?.Deserialize<UpdateSipAccountRequest>(SerializerOptions);
        }
        catch (JsonException)
        {
            body = null;
        }

        if (body is null)
        {
            return Bad("A JSON body is required.");
        }

        if (string.IsNullOrWhiteSpace(body.DisplayName))
        {
            return Bad("displayName is required.");
        }

        // An update must not move an account onto an unsupported method either (#111). An
        // omitted method keeps the stored one, so an already-unsupported account can still be
        // edited towards a supported configuration.
        if (SipAuthMethodValidation.Reject(body.AuthMethod ?? account.Connection.Authentication.Method) is { } unsupported)
        {
            return unsupported;
        }

        // Reuse the existing authentication so omitted secrets are kept rather than dropped.
        if (!_connectionFactory.TryBuild(body, account.Connection.Authentication, out var connection, out var error))
        {
            return Bad(error!);
        }

        var maxConcurrentCalls = body.MaxConcurrentCalls ?? account.MaxConcurrentCalls;
        if (maxConcurrentCalls < 1)
        {
            return Bad("maxConcurrentCalls must be at least 1.");
        }

        // Omitted keeps, an empty list clears — the same rule maxConcurrentCalls follows, and the only
        // way back to an undivided trunk.
        if (!CallQuotaValidation.TryBuild(body.CallQuotas, out var callQuotas, out var quotaError))
        {
            return Bad(quotaError!);
        }

        account.Reconfigure(
            body.DisplayName!,
            connection!,
            maxConcurrentCalls,
            body.CallQuotas is null ? account.CallQuotas : callQuotas);
        await store.UpdateAsync(account, cancellationToken).ConfigureAwait(false);

        // Credential, endpoint or capacity changes reconnect the live channel (#110).
        var runtimeFailure = await _runtime.ReconcileAsync(account, cancellationToken).ConfigureAwait(false);
        return runtimeFailure ?? new HostAdminApiResponse(200, SipAccountResponse.FromDomain(account));
    }

    private static HostAdminApiResponse Bad(string message) => new(400, new { error = message });
}
