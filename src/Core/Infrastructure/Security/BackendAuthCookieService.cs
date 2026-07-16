using Callora.Core.Application.Policies;
using Callora.Core.Extensibility;

namespace Callora.Core.Infrastructure.Security;

[CalloraInternal("Admin auth-cookie issuance — login enforcement, not a plugin contract (REV2 §7.2)")]
public static class BackendAuthCookieService
{
    public static string ResolveCookieName(BackendHostOptions options)
    {
        return string.IsNullOrWhiteSpace(options.AuthCookieName)
            ? "callora_admin_auth"
            : options.AuthCookieName.Trim();
    }

    public static void AppendAuthCookie(
        HttpResponse response,
        BackendHostOptions options,
        string token,
        TimeSpan lifetime,
        bool isHttps)
    {
        response.Cookies.Append(
            ResolveCookieName(options),
            token,
            CreateCookieOptions(options, isHttps, DateTimeOffset.UtcNow.Add(lifetime)));
    }

    public static void ClearAuthCookie(HttpResponse response, BackendHostOptions options, bool isHttps)
    {
        response.Cookies.Append(
            ResolveCookieName(options),
            string.Empty,
            CreateCookieOptions(options, isHttps, DateTimeOffset.UtcNow.AddDays(-1)));
    }

    private static CookieOptions CreateCookieOptions(
        BackendHostOptions options,
        bool isHttps,
        DateTimeOffset expiresAtUtc)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = options.AuthCookieRequireHttps || isHttps,
            SameSite = SameSiteMode.Lax,
            Expires = expiresAtUtc,
            IsEssential = true,
            Path = "/"
        };
    }
}
