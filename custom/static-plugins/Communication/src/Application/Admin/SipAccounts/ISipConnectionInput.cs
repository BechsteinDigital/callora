using Callora.Plugin.Communication.Domain.Accounts;

namespace Callora.Plugin.Communication.Application.Admin.SipAccounts;

/// <summary>
/// The connection fields shared by the create and update request bodies, so
/// <see cref="SipAccountConnectionFactory"/> can build a <see cref="SipConnection"/> from either.
/// Secret material (<see cref="Password"/>, <see cref="ClientCertificate"/>) is optional on update —
/// omitting it keeps the stored reference.
/// </summary>
internal interface ISipConnectionInput
{
    string? Host { get; }
    int? Port { get; }
    SipTransport? Transport { get; }
    SipAuthMethod? AuthMethod { get; }
    SipAccountMode? Mode { get; }
    string? Username { get; }
    string? Password { get; }
    string? AuthId { get; }
    string? ClientCertificate { get; }
    string? ClientCertificateSecretRef { get; }
    int? RegistrationExpirySeconds { get; }
}
