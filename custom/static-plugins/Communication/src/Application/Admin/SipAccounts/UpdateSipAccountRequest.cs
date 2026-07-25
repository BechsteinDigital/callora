using Callora.Plugin.Communication.Domain.Accounts;

namespace Callora.Plugin.Communication.Application.Admin.SipAccounts;

/// <summary>
/// Body for updating a SIP account (PUT). Replaces the account's editable configuration; the
/// enabled/status lifecycle is managed by the enable/disable routes, not here. Secret material
/// (<see cref="Password"/>, <see cref="ClientCertificate"/>) is optional — omit it to keep the stored
/// credential, provide it to rotate. <see cref="MaxConcurrentCalls"/> omitted keeps the current value.
/// </summary>
public sealed record UpdateSipAccountRequest(
    string? DisplayName,
    string? Host,
    int? Port,
    SipTransport? Transport,
    SipAuthMethod? AuthMethod,
    SipAccountMode? Mode,
    string? Username,
    string? Password,
    string? AuthId,
    string? ClientCertificate,
    string? ClientCertificateSecretRef,
    int? RegistrationExpirySeconds,
    int? MaxConcurrentCalls) : ISipConnectionInput;
