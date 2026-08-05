using Callora.Core.Application.Plugins.Contracts;
using Callora.Plugin.Communication.Application.Voice;
using Callora.Plugin.Communication.Domain.Accounts;

namespace Callora.Plugin.Communication.Application.Admin.SipAccounts;

/// <summary>
/// Refuses SIP accounts the voice provider cannot connect, at the edge (#111).
/// <para>
/// <c>422 Unprocessable Content</c> rather than <c>400</c>: the request is well-formed and the
/// authentication method is a legitimate SIP deployment — this platform just cannot operate it
/// yet. The distinction matters to a client, because a 400 says "fix your request" while a 422
/// says "this is understood but unsupported", and the message names the upstream gap.
/// </para>
/// </summary>
internal static class SipAuthMethodValidation
{
    /// <summary>
    /// Returns the refusal response when <paramref name="method"/> cannot be connected, or null
    /// when the account may be created or updated.
    /// </summary>
    public static HostAdminApiResponse? Reject(SipAuthMethod? method)
    {
        var effective = method ?? SipAuthMethod.Digest;
        if (SipAuthMethodSupport.DescribeUnsupported(effective) is not { } reason)
        {
            return null;
        }

        return new HostAdminApiResponse(422, new
        {
            error = reason,
            authMethod = effective.ToString(),
            supportedAuthMethods = SipAuthMethodSupport.Supported.Select(x => x.ToString()).ToArray()
        });
    }
}
