using System.Text.Json;
using System.Text.Json.Serialization;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Secrets.Contracts;
using Callora.Plugin.Communication.Application.Accounts;
using Callora.Plugin.Communication.Domain.Accounts;

namespace Callora.Plugin.Communication.Application.Admin.SipAccounts;

/// <summary>
/// Handles <c>POST sip-accounts</c> — creates a registering (digest) SIP account in the caller's
/// workspace. The plaintext password is protected into the secret store immediately; only its
/// reference is persisted, and the response never carries the password.
/// </summary>
public sealed class CreateSipAccountRouteHandler(
    ISipAccountStore store,
    IPluginDataProtector dataProtector,
    string pluginId) : IHostAdminApiRouteHandler
{
    private const int DefaultPort = 5060;
    private const int DefaultRegistrationExpirySeconds = 300;

    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    /// <inheritdoc />
    public async ValueTask<HostAdminApiResponse> HandleAsync(
        HostAdminApiRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!SipAccountAdminScope.TryResolve(request, out var workspaceKey, out var error))
        {
            return error!;
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

        if (string.IsNullOrWhiteSpace(body.Host))
        {
            return Bad("host is required.");
        }

        if (string.IsNullOrWhiteSpace(body.Username))
        {
            return Bad("username is required.");
        }

        if (string.IsNullOrWhiteSpace(body.Password))
        {
            return Bad("password is required.");
        }

        var port = body.Port ?? DefaultPort;
        if (port is < 1 or > 65535)
        {
            return Bad("port must be between 1 and 65535.");
        }

        var expiry = body.RegistrationExpirySeconds ?? DefaultRegistrationExpirySeconds;
        if (expiry < 1)
        {
            return Bad("registrationExpirySeconds must be at least 1.");
        }

        var maxConcurrentCalls = body.MaxConcurrentCalls ?? 1;
        if (maxConcurrentCalls < 1)
        {
            return Bad("maxConcurrentCalls must be at least 1.");
        }

        // Protect the password on receipt; only the reference is stored (never the plaintext).
        var passwordSecretRef = dataProtector.Protect(pluginId, body.Password!);
        var authentication = new DigestAuthentication(body.Username!, body.AuthId, passwordSecretRef);
        var connection = new SipConnection(
            body.Host!, port, body.Transport ?? SipTransport.Udp, SipAccountMode.Register, authentication, expiry);
        var account = new SipAccount(
            Guid.NewGuid().ToString("n"),
            workspaceKey,
            body.DisplayName!,
            connection,
            maxConcurrentCalls,
            body.Enabled ?? true);

        await store.AddAsync(account, cancellationToken).ConfigureAwait(false);
        return new HostAdminApiResponse(201, SipAccountResponse.FromDomain(account));
    }

    private static HostAdminApiResponse Bad(string message) => new(400, new { error = message });
}
