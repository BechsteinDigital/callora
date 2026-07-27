using Callora.Core.Infrastructure.Security;

namespace Callora.Core.Tests.Infrastructure.Security;

public class BackendCsrfValidatorTests
{
    private static readonly string[] NoAllowed = [];
    private const string Own = "https://admin.example.com";

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    [InlineData("TRACE")]
    public void SafeMethodsAreNeverForbidden(string method)
    {
        // Even cross-origin with a cookie: safe methods do not change state.
        Assert.False(BackendCsrfValidator.IsForbidden(
            method, hasAuthCookie: true, "https://evil.test", refererHeader: null, Own, NoAllowed));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public void EveryUnsafeMethodIsChecked_CrossOriginCookieIsForbidden(string method)
    {
        // The guard uses a safe-list, so all state-changing verbs are covered.
        Assert.True(BackendCsrfValidator.IsForbidden(
            method, hasAuthCookie: true, "https://evil.test", refererHeader: null, Own, NoAllowed));
    }

    [Fact]
    public void HeaderAuthenticatedRequestWithoutCookieIsExempt()
    {
        // No auth cookie => Bearer/API-key client => not a CSRF vector.
        Assert.False(BackendCsrfValidator.IsForbidden(
            "POST", hasAuthCookie: false, "https://evil.test", refererHeader: null, Own, NoAllowed));
    }

    [Fact]
    public void SameOriginCookieMutationIsAllowed()
    {
        Assert.False(BackendCsrfValidator.IsForbidden(
            "POST", hasAuthCookie: true, Own, refererHeader: null, Own, NoAllowed));
    }

    [Fact]
    public void CrossOriginCookieMutationIsForbidden()
    {
        Assert.True(BackendCsrfValidator.IsForbidden(
            "POST", hasAuthCookie: true, "https://evil.test", refererHeader: null, Own, NoAllowed));
    }

    [Fact]
    public void CookieMutationWithoutOriginOrRefererIsForbidden()
    {
        // Fail-closed: a cookie-authenticated mutation with no verifiable source is rejected.
        Assert.True(BackendCsrfValidator.IsForbidden(
            "POST", hasAuthCookie: true, originHeader: null, refererHeader: null, Own, NoAllowed));
    }

    [Fact]
    public void RefererIsUsedWhenOriginHeaderIsAbsent()
    {
        Assert.False(BackendCsrfValidator.IsForbidden(
            "POST", hasAuthCookie: true, originHeader: null,
            "https://admin.example.com/admin/users", Own, NoAllowed));
    }

    [Fact]
    public void CrossOriginRefererIsForbidden()
    {
        Assert.True(BackendCsrfValidator.IsForbidden(
            "POST", hasAuthCookie: true, originHeader: null, "https://evil.test/attack", Own, NoAllowed));
    }

    [Fact]
    public void ExplicitlyAllowedOriginIsAccepted()
    {
        string[] allowed = ["https://shell.example.com"];
        Assert.False(BackendCsrfValidator.IsForbidden(
            "POST", hasAuthCookie: true, "https://shell.example.com", refererHeader: null, Own, allowed));
    }

    [Fact]
    public void OpaqueNullOriginIsForbidden()
    {
        // Sandboxed iframes / privacy-sensitive contexts send Origin: null.
        Assert.True(BackendCsrfValidator.IsForbidden(
            "POST", hasAuthCookie: true, "null", refererHeader: null, Own, NoAllowed));
    }

    [Fact]
    public void DefaultPortIsNormalizedAgainstExplicitPort()
    {
        // Origin header without :443 must still match a request origin carrying :443.
        Assert.False(BackendCsrfValidator.IsForbidden(
            "POST", hasAuthCookie: true, "https://admin.example.com", refererHeader: null,
            "https://admin.example.com:443", NoAllowed));
    }

    // --- IsForbiddenLogin: guards the cookie-issuing login POST (no prior cookie) ---

    [Theory]
    [InlineData("GET")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public void Login_SafeMethodIsNeverForbidden(string method)
    {
        Assert.False(BackendCsrfValidator.IsForbiddenLogin(
            method, "https://evil.test", refererHeader: null, Own, NoAllowed));
    }

    [Fact]
    public void Login_SameOriginPostIsAllowed()
    {
        Assert.False(BackendCsrfValidator.IsForbiddenLogin(
            "POST", Own, refererHeader: null, Own, NoAllowed));
    }

    [Fact]
    public void Login_CrossOriginBrowserPostIsForbidden()
    {
        // The core of the fix: a forged cross-site login plants a session the victim never chose.
        Assert.True(BackendCsrfValidator.IsForbiddenLogin(
            "POST", "https://evil.test", refererHeader: null, Own, NoAllowed));
    }

    [Fact]
    public void Login_WithoutAnySourceIsAllowed()
    {
        // A truly source-less request is a non-browser API client (curl, mobile) — no browser
        // session to hijack — so a programmatic login must keep working.
        Assert.False(BackendCsrfValidator.IsForbiddenLogin(
            "POST", originHeader: null, refererHeader: null, Own, NoAllowed));
    }

    [Fact]
    public void Login_OpaqueNullOriginIsForbidden()
    {
        // A present-but-opaque Origin (sandboxed iframe, file://) is a browser context we cannot
        // verify: reject fail-closed so the guard is not bypassable via an opaque origin.
        Assert.True(BackendCsrfValidator.IsForbiddenLogin(
            "POST", "null", refererHeader: null, Own, NoAllowed));
    }

    [Fact]
    public void Login_OpaqueNullOriginIsNotHealedBySameOriginReferer()
    {
        // The opaque Origin is authoritative over any Referer: a same-origin Referer must not
        // "heal" it into an allowed request, otherwise the fail-closed guard is bypassable.
        Assert.True(BackendCsrfValidator.IsForbiddenLogin(
            "POST", "null", $"{Own}/admin/login", Own, NoAllowed));
    }

    [Fact]
    public void Login_CrossOriginRefererIsForbiddenWhenOriginAbsent()
    {
        Assert.True(BackendCsrfValidator.IsForbiddenLogin(
            "POST", originHeader: null, "https://evil.test/attack", Own, NoAllowed));
    }

    [Fact]
    public void Login_ExplicitlyAllowedOriginIsAccepted()
    {
        // Split-origin deployment: the admin shell is served from a different host than the API.
        string[] allowed = ["https://shell.example.com"];
        Assert.False(BackendCsrfValidator.IsForbiddenLogin(
            "POST", "https://shell.example.com", refererHeader: null, Own, allowed));
    }
}
