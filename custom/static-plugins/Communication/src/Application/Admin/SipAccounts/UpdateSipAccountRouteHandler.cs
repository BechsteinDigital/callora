using System.Text.Json;
using System.Text.Json.Serialization;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Secrets.Contracts;
using Callora.Plugin.Communication.Application.Accounts;

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
    string pluginId) : IHostAdminApiRouteHandler
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly SipAccountConnectionFactory _connectionFactory = new(dataProtector, pluginId);

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

        account.Reconfigure(body.DisplayName!, connection!, maxConcurrentCalls);
        await store.UpdateAsync(account, cancellationToken).ConfigureAwait(false);
        return new HostAdminApiResponse(200, SipAccountResponse.FromDomain(account));
    }

    private static HostAdminApiResponse Bad(string message) => new(400, new { error = message });
}
