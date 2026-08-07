using Callora.Plugin.Communication.Domain.Accounts;

namespace Callora.Plugin.Communication.Application.Admin.SipAccounts;

/// <summary>
/// Body for creating a SIP account. Supports all three authentication methods:
/// <list type="bullet">
/// <item>digest (default, register mode): <see cref="Username"/> + <see cref="Password"/> required;</item>
/// <item>IP-authenticated trunk: no credentials, defaults to <see cref="SipAccountMode.Trunk"/>;</item>
/// <item>mutual-TLS: <see cref="ClientCertificate"/> material (or an existing
/// <see cref="ClientCertificateSecretRef"/>) required.</item>
/// </list>
/// Secret material (password, certificate) is sent once and immediately protected into the secret
/// store — never persisted or echoed back. <see cref="Mode"/> defaults per method and is validated by
/// <see cref="SipConnection"/> (e.g. a registering connection cannot use IP authentication). A
/// credentialed trunk (<see cref="SipAccountMode.Trunk"/> + digest) may carry an
/// <see cref="OutboundProxy"/> and an <see cref="InboundNumbers"/> DID whitelist.
/// </summary>
public sealed record CreateSipAccountRequest(
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
    string? OutboundProxy,
    IReadOnlyList<string>? InboundNumbers,
    int? MaxConcurrentCalls,
    bool? Enabled,
    IReadOnlyList<CallQuotaRequest>? CallQuotas = null) : ISipConnectionInput;
