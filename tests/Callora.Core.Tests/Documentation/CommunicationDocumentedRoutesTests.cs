using System.Text.RegularExpressions;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Tests.Cli;
using Callora.Core.Tests.Communication;
using Callora.Plugin.Communication;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Callora.Core.Tests.Documentation;

/// <summary>
/// Keeps the Communication page honest about the API it describes. Documentation that promises an
/// endpoint the build does not serve is worse than none: it reads as a readiness claim, and someone
/// plans around it.
/// </summary>
/// <remarks>
/// <para>
/// The routes come from starting the plugin, not from a list maintained beside it. A list would be
/// one more thing to forget; a started plugin cannot disagree with itself.
/// </para>
/// <para>
/// The check runs one way on purpose. Every documented route must exist; not every route must be
/// documented, because an endpoint can legitimately be plumbing. Only the direction that misleads a
/// reader is enforced.
/// </para>
/// </remarks>
public sealed class CommunicationDocumentedRoutesTests
{
    [Fact]
    public async Task EveryDocumentedEndpointIsARegisteredRoute()
    {
        var documented = DocumentedRoutes();
        Assert.NotEmpty(documented);

        var registered = await RegisteredRoutesAsync();

        var missing = documented
            .Where(route => !registered.Any(actual => Matches(actual, route)))
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "The Communication page documents endpoints the plugin does not serve:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, missing.Select(route => $"  {route.Method} {route.Path}"))
            + Environment.NewLine
            + "Registered:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, registered.Select(route => $"  {route.Method} {route.Path}")));
    }

    /// <summary>
    /// A documented path may carry a concrete id where the route declares a parameter, so
    /// <c>calls/abc/accept</c> matches <c>calls/{callId}/accept</c>. Segment counts still have to
    /// agree, which is what stops this matching anything.
    /// </summary>
    private static bool Matches((string Method, string Path) registered, (string Method, string Path) documented)
    {
        if (!string.Equals(registered.Method, documented.Method, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var actual = registered.Path.Split('/');
        var expected = documented.Path.Split('/');
        if (actual.Length != expected.Length)
        {
            return false;
        }

        return !actual.Where((segment, index) =>
            !segment.StartsWith('{') &&
            !string.Equals(segment, expected[index], StringComparison.OrdinalIgnoreCase)).Any();
    }

    private static IReadOnlyList<(string Method, string Path)> DocumentedRoutes()
    {
        var page = File.ReadAllText(Path.Combine(
            ScaffoldedPluginFixture.ResolveRepositoryRoot(),
            "docs-site", "users", "communication.md"));

        return
        [
            .. Regex
                .Matches(page, @"(GET|POST|PUT|DELETE|PATCH) /api/ext/admin/plugins/communication/([^\s`)]+)")
                .Select(match => (match.Groups[1].Value, match.Groups[2].Value.TrimEnd('.', ',')))
                .Distinct(),
        ];
    }

    private static async Task<IReadOnlyList<(string Method, string Path)>> RegisteredRoutesAsync()
    {
        // The full deployment: with persistence and WebRTC on, so the page may document everything
        // the plugin can serve rather than only its degraded surface.
        var context = new CapturingHostPluginContext(
            hasDbFactory: true,
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["WebRtc:Enabled"] = "true" })
                .Build());
        await new CommunicationPlugin().StartAsync(context);

        var contributor = (IHostAdminApiExtensionContributor)context.Exports[typeof(IHostAdminApiExtensionContributor)];

        return [.. contributor.Routes.Select(route => (route.HttpMethod, route.RouteTemplate))];
    }
}
