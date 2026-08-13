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
        var sources = Sources("script-src");

        Assert.Contains("'self'", sources);
        Assert.DoesNotContain(sources, source => source.StartsWith("http", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("*", sources);
    }

    [Fact]
    public void EvalIsNotAvailable()
    {
        // The difference between a bundle running its reviewed code and running a payload it fetched.
        //
        // Verglichen wird TOKENWEISE und nicht als Teilzeichenkette: 'wasm-unsafe-eval' enthält
        // 'unsafe-eval', ist aber das Gegenteil davon — es erlaubt ausschließlich das Übersetzen
        // von WebAssembly und weiterhin kein eval(). Ein Test auf den Teilstring hätte die enge
        // Erlaubnis genauso abgelehnt wie die weite und damit zu der Änderung gedrängt, die er
        // verhindern soll.
        Assert.DoesNotContain("'unsafe-eval'", Sources("script-src"));
    }

    /// <summary>
    /// WebAssembly braucht eine eigene Erlaubnis, und ohne sie weigert sich jeder Browser, ein
    /// WASM-Modul zu übersetzen — was den Hintergrund-Weichzeichner ausfallen ließ, dessen
    /// Segmentierungsmodell genau das ist. Die Fehlermeldung des Browsers nennt dabei
    /// <c>unsafe-eval</c>; wer sie wörtlich nimmt, öffnet <c>eval()</c> in beiden Shells, um ein
    /// Modell zu laden.
    /// </summary>
    [Fact]
    public void WebAssemblyMayCompileWithoutOpeningEval()
    {
        var sources = Sources("script-src");

        Assert.Contains("'wasm-unsafe-eval'", sources);
        Assert.DoesNotContain("'unsafe-eval'", sources);
    }

    private static string[] Sources(string directive) =>
        Directive(directive)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(1)
            .ToArray();

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
