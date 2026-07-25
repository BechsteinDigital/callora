using Callora.Core.Application.Secrets.Contracts;
using Callora.Plugin.Communication.Domain.Accounts;

namespace Callora.Plugin.Communication.Application.Admin.SipAccounts;

/// <summary>
/// Builds a validated <see cref="SipConnection"/> from a create/update request across all three
/// authentication methods, protecting any supplied secret material (digest password, mutual-TLS
/// certificate) into the secret store on the way. On update, an <c>existing</c> authentication lets
/// omitted secrets fall back to the stored reference (rotate only when new material is sent). Invalid
/// combinations surface as a message (returns <see langword="false"/>) rather than an exception.
/// </summary>
internal sealed class SipAccountConnectionFactory(IPluginDataProtector dataProtector, string pluginId)
{
    private const int DefaultPort = 5060;
    private const int DefaultRegistrationExpirySeconds = 300;

    /// <summary>
    /// Attempts to build the connection. <paramref name="existing"/> is the current authentication on
    /// update (null on create) so omitted secrets can be preserved.
    /// </summary>
    public bool TryBuild(
        ISipConnectionInput input,
        SipAuthentication? existing,
        out SipConnection? connection,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(input);

        connection = null;
        error = null;

        if (string.IsNullOrWhiteSpace(input.Host))
        {
            error = "host is required.";
            return false;
        }

        var port = input.Port ?? DefaultPort;
        if (port is < 1 or > 65535)
        {
            error = "port must be between 1 and 65535.";
            return false;
        }

        var transport = input.Transport ?? SipTransport.Udp;
        var authMethod = input.AuthMethod ?? SipAuthMethod.Digest;
        var mode = input.Mode
            ?? (authMethod == SipAuthMethod.IpAuthenticated ? SipAccountMode.Trunk : SipAccountMode.Register);

        if (!TryBuildAuthentication(input, authMethod, existing, out var authentication, out error))
        {
            return false;
        }

        // Register connections need an expiry (default it); trunks must not carry one.
        var expiry = input.RegistrationExpirySeconds
            ?? (mode == SipAccountMode.Register ? DefaultRegistrationExpirySeconds : (int?)null);

        try
        {
            connection = new SipConnection(input.Host!, port, transport, mode, authentication!, expiry);
        }
        catch (ArgumentException ex)
        {
            // Domain invariants (e.g. register + IP auth, trunk + expiry) become a 400, not a 500.
            error = ex.Message;
            return false;
        }

        return true;
    }

    private bool TryBuildAuthentication(
        ISipConnectionInput input,
        SipAuthMethod method,
        SipAuthentication? existing,
        out SipAuthentication? authentication,
        out string? error)
    {
        authentication = null;
        error = null;

        switch (method)
        {
            case SipAuthMethod.Digest:
                var username = string.IsNullOrWhiteSpace(input.Username)
                    ? (existing as DigestAuthentication)?.Username
                    : input.Username;
                if (string.IsNullOrWhiteSpace(username))
                {
                    error = "username is required for digest authentication.";
                    return false;
                }

                var passwordSecretRef = string.IsNullOrWhiteSpace(input.Password)
                    ? (existing as DigestAuthentication)?.PasswordSecretRef
                    : dataProtector.Protect(pluginId, input.Password!);
                if (string.IsNullOrWhiteSpace(passwordSecretRef))
                {
                    error = "password is required for digest authentication.";
                    return false;
                }

                var authId = input.AuthId ?? (existing as DigestAuthentication)?.AuthId;
                authentication = new DigestAuthentication(username!, authId, passwordSecretRef!);
                return true;

            case SipAuthMethod.IpAuthenticated:
                authentication = IpAuthentication.Instance;
                return true;

            case SipAuthMethod.MutualTls:
                string? certificateSecretRef;
                if (!string.IsNullOrWhiteSpace(input.ClientCertificateSecretRef))
                {
                    certificateSecretRef = input.ClientCertificateSecretRef;
                }
                else if (!string.IsNullOrWhiteSpace(input.ClientCertificate))
                {
                    certificateSecretRef = dataProtector.Protect(pluginId, input.ClientCertificate!);
                }
                else
                {
                    certificateSecretRef = (existing as MutualTlsAuthentication)?.ClientCertificateSecretRef;
                }

                if (string.IsNullOrWhiteSpace(certificateSecretRef))
                {
                    error = "clientCertificate (or clientCertificateSecretRef) is required for mutual-TLS authentication.";
                    return false;
                }

                authentication = new MutualTlsAuthentication(certificateSecretRef!);
                return true;

            default:
                error = $"Unsupported authentication method '{method}'.";
                return false;
        }
    }
}
