using Callora.Core.Application.Policies;
using Xunit;

namespace Callora.Core.Tests.Application.Policies;

/// <summary>
/// The default policy is a security decision, not a formatting choice: plugin admin bundles run in
/// the shell's document, and this is what bounds where their code and data may come from and go.
/// These pin the directives that would silently give that up.
/// </summary>
public sealed class BackendContentSecurityPolicyTests
{
    [Fact]
    public void ScriptsAreSameOriginOnly()
    {
        // A bundle that could load from an arbitrary origin makes the package signature pointless:
        // the reviewed code would be a loader for code nobody reviewed.
        Assert.Contains("script-src 'self';", BackendContentSecurityPolicy.Default, StringComparison.Ordinal);
    }

    [Fact]
    public void EvalIsNotAvailable()
    {
        // The difference between a bundle running its reviewed code and running a payload it fetched.
        Assert.DoesNotContain("unsafe-eval", BackendContentSecurityPolicy.Default, StringComparison.Ordinal);
    }

    [Fact]
    public void ScriptsCarryNoInlineAllowance()
    {
        // Inline styles are conceded for Vue; inline scripts are not, because that concession would
        // reopen exactly what script-src closes.
        var scriptSrc = Directive("script-src");

        Assert.DoesNotContain("unsafe-inline", scriptSrc, StringComparison.Ordinal);
    }

    [Fact]
    public void TheShellCannotBeFramed()
    {
        Assert.Contains("frame-ancestors 'none'", BackendContentSecurityPolicy.Default, StringComparison.Ordinal);
    }

    [Fact]
    public void ConnectionsStayOnThisOriginIncludingWebSockets()
    {
        // Same-origin plus ws/wss: the signalling and media sockets are on this host, and an
        // unrestricted connect-src would be the exfiltration path the rest of the policy closes.
        var connectSrc = Directive("connect-src");

        Assert.Contains("'self'", connectSrc, StringComparison.Ordinal);
        Assert.DoesNotContain("*", connectSrc, StringComparison.Ordinal);
    }

    [Fact]
    public void ObjectsAndBaseUriAreClosed()
    {
        Assert.Contains("object-src 'none'", BackendContentSecurityPolicy.Default, StringComparison.Ordinal);
        Assert.Contains("base-uri 'self'", BackendContentSecurityPolicy.Default, StringComparison.Ordinal);
    }

    private static string Directive(string name) =>
        BackendContentSecurityPolicy.Default
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Single(part => part.StartsWith(name + " ", StringComparison.Ordinal));
}
