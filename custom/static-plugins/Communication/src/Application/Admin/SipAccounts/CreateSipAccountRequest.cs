using Callora.Plugin.Communication.Domain.Accounts;

namespace Callora.Plugin.Communication.Application.Admin.SipAccounts;

/// <summary>
/// Body for creating a registering (digest) SIP account. The password is sent once in plaintext and
/// immediately protected into the secret store by the handler — it is never persisted or echoed back.
/// v1 supports digest/register accounts (the connectable kind); IP/mutual-TLS trunks are out of scope.
/// </summary>
/// <param name="DisplayName">Operator-facing name (required).</param>
/// <param name="Host">SIP registrar host (required).</param>
/// <param name="Port">Signalling port (1–65535); defaults to 5060 when omitted.</param>
/// <param name="Transport">Signalling transport; defaults to <see cref="SipTransport.Udp"/>.</param>
/// <param name="Username">Digest username (required).</param>
/// <param name="Password">Digest password in plaintext (required); protected on receipt.</param>
/// <param name="AuthId">Optional distinct authentication id (defaults to the username).</param>
/// <param name="RegistrationExpirySeconds">Requested registration expiry (≥ 1); defaults to 300.</param>
/// <param name="MaxConcurrentCalls">Max simultaneous calls (≥ 1); defaults to 1.</param>
/// <param name="Enabled">Whether the account is provisioned immediately; defaults to true.</param>
public sealed record CreateSipAccountRequest(
    string? DisplayName,
    string? Host,
    int? Port,
    SipTransport? Transport,
    string? Username,
    string? Password,
    string? AuthId,
    int? RegistrationExpirySeconds,
    int? MaxConcurrentCalls,
    bool? Enabled);
