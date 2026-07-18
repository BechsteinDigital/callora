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
}
