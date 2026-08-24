using Callora.Core.Application.Http.Contracts;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Security;
using Callora.Core.Infrastructure.Http;
using Callora.Core.Infrastructure.Security;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Callora.Core.Tests.Infrastructure.Security;

/// <summary>
/// "May this caller perform this action" is asked on two paths, and they used to answer it
/// differently. <see cref="EndpointAuthorizationExtensions"/> allows a super admin by ROLE,
/// and accepts a scope claim as well as a permission claim; the plugin route pipeline
/// compared permission claims inline and did neither.
/// </summary>
/// <remarks>
/// It did not show as a bug because <c>BackendRbacDatabaseSeeder</c> grants the superadmin
/// role a <c>"*"</c> permission claim, so the role holder passes the inline rule anyway. The
/// two rules agree by coincidence of the current seeding, not by construction — and anything
/// that grants the role without that claim, or authorizes by scope, is accepted by host
/// endpoints and rejected by plugin routes.
/// </remarks>
public sealed class BothAuthorizationPathsAnswerTheSameTests
{
    [Theory]
    [InlineData("/api/test-plugin/ping")]
    [InlineData("/host/guarded")]
    public async Task A_super_admin_by_role_passes_without_a_wildcard_claim(string path)
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Roles", BackendRoles.SuperAdmin);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/test-plugin/ping")]
    [InlineData("/host/guarded")]
    public async Task A_scope_claim_authorizes_the_same_as_a_permission_claim(string path)
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-Scopes", "test.read");

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/test-plugin/ping")]
    [InlineData("/host/guarded")]
    [InlineData("/mvc/guarded")]
    public async Task A_refusal_names_the_key_that_was_missing(string path)
    {
        // Without this an operator debugging a role grant bisects the 37-key catalogue by
        // hand. It leaks nothing: the caller already knows which endpoint it called, and
        // docs-site/reference/permissions.md publishes every key.
        await using var app = await CreateAppAsync();

        var response = await app.GetTestClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);

        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(
            problem.RootElement.TryGetProperty("missingPermission", out var missing),
            $"{path} answered 403 without naming the missing permission.");
        Assert.Equal("test.read", missing.GetString());
    }

    [Fact]
    public void NoOtherSiteDecidesPermissions()
    {
        // The guard against a fourth path. Two rules agreeing by coincidence is what this
        // whole class of bug was: the divergence survived because the seeded "*" grant made
        // both rules answer alike for the only principal anyone tested with.
        // Two files may read a permission claim, each for a reason that is not a decision:
        //   EndpointAuthorizationExtensions — it IS the decision, the one everyone else asks.
        //   BackendClaimsTransformation     — it STAMPS claims and reads only to avoid
        //                                     duplicates; nothing is denied there.
        // Named rather than pattern-matched, because a clever pattern would eventually be
        // wrong in the other direction and let a real second rule through.
        string[] allowed = ["EndpointAuthorizationExtensions.cs", "BackendClaimsTransformation.cs"];

        var root = Callora.Core.Tests.Cli.ScaffoldedPluginFixture.ResolveRepositoryRoot();
        var offenders = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !allowed.Any(name => path.EndsWith(name, StringComparison.Ordinal)))
            .Where(path => File.ReadAllText(path)
                .Contains("HasClaim(BackendClaimTypes.Permission", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"These compare permission claims instead of asking UserHasPermission: {string.Join(", ", offenders)}");
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var catalog = new StaticPluginCatalog(new Dictionary<Type, IReadOnlyList<object>>
        {
            [typeof(IApiController)] = [new TestPluginAdminController()]
        });
        var dataSource = new PluginApiEndpointDataSource(catalog, NullLogger<PluginApiEndpointDataSource>.Instance);
        dataSource.Refresh();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services
            .AddAuthentication("Header")
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Header", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IPluginAvailabilityEvaluator>(new StaticPluginAvailabilityEvaluator());
        builder.Services.AddControllers().AddApplicationPart(typeof(GuardedTestController).Assembly);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        ((IEndpointRouteBuilder)app).DataSources.Add(dataSource);

        // The host side of the same question, guarded by the same key the plugin route uses.
        app.MapGet("/host/guarded", () => Results.Ok(new { ok = true }))
            .RequirePermission("test.read");

        // And the MVC side. It already shared the decision — only its refusal was mute.
        app.MapControllers();

        await app.StartAsync();
        return app;
    }
}

[ApiController]
[Route("mvc")]
public sealed class GuardedTestController : ControllerBase
{
    [HttpGet("guarded")]
    [CalloraPermission("test.read")]
    public IActionResult Guarded() => Ok(new { ok = true });
}
