using System.Text.Json;
using System.Text.Json.Serialization;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Secrets.Contracts;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Application.Voice;
using Callora.Plugin.Communication.Domain.Accounts;

namespace Callora.Plugin.Communication.Application.Admin.SipAccounts;

/// <summary>
/// Handles <c>POST sip-accounts</c> — creates a SIP account (digest / IP-trunk / mutual-TLS) in the
/// caller's workspace. Any secret material is protected into the secret store immediately; only
/// references are persisted and the response never carries a credential.
/// </summary>
public sealed class CreateSipAccountRouteHandler(
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

        CreateSipAccountRequest? body;
        try
        {
            body = request.Body?.Deserialize<CreateSipAccountRequest>(SerializerOptions);
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

        if (!_connectionFactory.TryBuild(body, existing: null, out var connection, out var error))
        {
            return Bad(error!);
        }

        var maxConcurrentCalls = body.MaxConcurrentCalls ?? 1;
        if (maxConcurrentCalls < 1)
        {
            return Bad("maxConcurrentCalls must be at least 1.");
        }

        var account = new SipAccount(
            Guid.NewGuid().ToString("n"),
            workspaceKey,
            body.DisplayName!,
            connection!,
            maxConcurrentCalls,
            body.Enabled ?? true);

        await store.AddAsync(account, cancellationToken).ConfigureAwait(false);

        // A created-and-enabled account must register now, not at the next restart (#110).
        var runtimeFailure = await _runtime.ReconcileAsync(account, cancellationToken).ConfigureAwait(false);
        return runtimeFailure ?? new HostAdminApiResponse(201, SipAccountResponse.FromDomain(account));
    }

    private static HostAdminApiResponse Bad(string message) => new(400, new { error = message });
}
