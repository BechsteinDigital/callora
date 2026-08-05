namespace Callora.Core.Application.Plugins.Contracts;

/// <summary>
/// Where a surface identity provider's declared credential is read from
/// (ADR-017 §4). A provider sees exactly the sources it declared — never a raw
/// header or cookie collection, and never the host's own session cookie.
/// </summary>
public enum SurfaceIdentityCredentialKind
{
    /// <summary>A request header, for example <c>X-Crm-Session</c>.</summary>
    Header = 0,

    /// <summary>A request cookie, for example <c>crm_session</c>.</summary>
    Cookie = 1,
}
