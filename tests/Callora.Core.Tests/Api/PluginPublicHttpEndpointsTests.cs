using Callora.Administration.Api;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;

namespace Callora.Core.Tests.Api;

/// <summary>
/// Walking skeleton for the reserved <c>/public/{pluginId}/…</c> prefix:
/// plugin public HTTP endpoints — anonymous at the platform layer, handler-validated.
/// Covers GET/POST happy paths (route values, query, body), redirect responses,
/// unknown routes (404, no info leak), handler exceptions (500, no detail), and
/// body size enforcement (413). Also covers the route matcher directly (method
/// discrimination, route value extraction, no-match cases).
/// </summary>
public sealed class PluginPublicHttpEndpointsTests
{
    // ------------------------------------------------------------------
    // GET happy path: status/content-type/body written correctly
    // ------------------------------------------------------------------

    [Fact]
    public async Task Get_WithMatchingRoute_ReturnsHandlerHtmlResponse()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/public/forms-plugin/join/invite-abc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("invite-abc", body);
    }

    // ------------------------------------------------------------------
    // POST happy path: body + query + route values reach handler; response written
    // ------------------------------------------------------------------

    [Fact]
    public async Task Post_WithBodyQueryAndRouteValues_HandlerReceivesAllAndResponds()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var content = new StringContent("name=Alice&accept=true", Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await client.PostAsync("/public/forms-plugin/submit/form-7?ref=email", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // Handler echoes back what it received to prove all three channels are wired.
        Assert.Contains("formId=form-7", body);
        Assert.Contains("ref=email", body);
        Assert.Contains("name=Alice", body);
    }

    // ------------------------------------------------------------------
    // Body size cap: oversized bodies → 413 before the handler runs
    // ------------------------------------------------------------------

    [Fact]
    public async Task Post_WithBodyOverLimit_Returns413()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        // > 1 MB payload; ContentLength is set by StringContent, exercising the guard.
        var oversized = new string('x', (1 * 1024 * 1024) + 1);
        var content = new StringContent(oversized, Encoding.UTF8, "text/plain");
        var response = await client.PostAsync("/public/forms-plugin/submit/form-7", content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Post_ChunkedBodyOverLimit_Returns413()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        // Streamed content with no known length forces Transfer-Encoding: chunked
        // (no ContentLength header) — the case the old guard missed. The capped read
        // must still reject it before reaching the handler.
        var content = new StreamContent(new EndlessChunkStream(2 * 1024 * 1024));
        var request = new HttpRequestMessage(HttpMethod.Post, "/public/forms-plugin/submit/form-7")
        {
            Content = content,
        };
        request.Headers.TransferEncodingChunked = true;

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithSmallBody_IsDeliveredToHandler()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var content = new StringContent("hello=world", Encoding.UTF8, "text/plain");
        var response = await client.PostAsync("/public/forms-plugin/submit/form-7", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("body=hello=world", body);
    }

    // ------------------------------------------------------------------
    // Header allowlist: Cookie/Authorization stripped, Content-Type forwarded
    // ------------------------------------------------------------------

    [Fact]
    public async Task Get_ForwardsOnlyAllowlistedHeaders_StripsCookieAndAuthorization()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/public/forms-plugin/headers");
        request.Headers.TryAddWithoutValidation("Cookie", "session=secret-session");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer secret-token");
        request.Headers.TryAddWithoutValidation("Accept-Language", "de-DE");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // Allowlisted header reaches the handler…
        Assert.Contains("Accept-Language=de-DE", body);
        // …while the session-bearing headers are stripped entirely.
        Assert.DoesNotContain("Cookie", body);
        Assert.DoesNotContain("Authorization", body);
        Assert.DoesNotContain("secret-session", body);
        Assert.DoesNotContain("secret-token", body);
    }

    // ------------------------------------------------------------------
    // Redirect: 302 + Location header written
    // ------------------------------------------------------------------

    [Fact]
    public async Task Get_RedirectRoute_WritesLocationHeaderAnd302()
    {
        await using var app = await CreateAppAsync();
        // Disable auto-redirect so we can inspect the 302 directly.
        var client = new HttpClient(app.GetTestServer().CreateHandler()) { BaseAddress = new Uri("http://localhost") };
        client = new HttpClient(
            app.GetTestServer().CreateHandler(),
            disposeHandler: false)
        {
            BaseAddress = new Uri("http://localhost")
        };

        var response = await client.GetAsync("/public/forms-plugin/go/to-success");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/success", response.Headers.Location?.ToString());
    }

    // ------------------------------------------------------------------
    // Unknown plugin or route → 404, no body (no info leak)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Get_UnknownPlugin_Returns404WithoutBody()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/public/does-not-exist/anything");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Empty(body);
    }

    [Fact]
    public async Task Get_UnknownRoute_Returns404WithoutBody()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/public/forms-plugin/unknown-path");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Empty(body);
    }

    [Fact]
    public async Task Post_WhereOnlyGetDeclared_Returns404()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        // The join/{token} route only accepts GET; POST should 404 (method mismatch → no match).
        var response = await client.PostAsync("/public/forms-plugin/join/invite-abc", new StringContent(string.Empty));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Handler exception → 500 without detail leak
    // ------------------------------------------------------------------

    [Fact]
    public async Task Get_HandlerThrows_Returns500WithoutExceptionDetail()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();

        var response = await client.GetAsync("/public/forms-plugin/boom");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        // No exception type or stack trace must reach the caller.
        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain("StackTrace", body);
    }

    // ------------------------------------------------------------------
    // AllowAnonymous: reachable without any auth header
    // ------------------------------------------------------------------

    [Fact]
    public async Task Get_WithoutAuthHeader_IsReachable()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        // Deliberately no Authorization header.
        client.DefaultRequestHeaders.Clear();

        var response = await client.GetAsync("/public/forms-plugin/join/open");

        // The handler returns 200 because no auth is required at platform level.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Route matcher unit tests
    // ------------------------------------------------------------------

    [Fact]
    public void RouteMatcher_MatchesMethodAndTemplate_ExtractsRouteValues()
    {
        var contributor = MakeContributor("my-plugin",
            new HostPublicHttpRouteRegistration("GET", "join/{token}", new NoOpHandler()));

        var match = PluginPublicHttpRouteMatcher.FindMatch([contributor], "my-plugin", "GET", "join/abc-123");

        Assert.NotNull(match);
        Assert.Equal("abc-123", match.RouteValues["token"]);
    }

    [Fact]
    public void RouteMatcher_MethodMismatch_ReturnsNull()
    {
        var contributor = MakeContributor("my-plugin",
            new HostPublicHttpRouteRegistration("GET", "form/{id}", new NoOpHandler()));

        var match = PluginPublicHttpRouteMatcher.FindMatch([contributor], "my-plugin", "POST", "form/42");

        Assert.Null(match);
    }

    [Fact]
    public void RouteMatcher_MethodMatchingIsCaseInsensitive()
    {
        var contributor = MakeContributor("my-plugin",
            new HostPublicHttpRouteRegistration("POST", "submit", new NoOpHandler()));

        var match = PluginPublicHttpRouteMatcher.FindMatch([contributor], "my-plugin", "post", "submit");

        Assert.NotNull(match);
    }

    [Fact]
    public void RouteMatcher_PluginIdMismatch_ReturnsNull()
    {
        var contributor = MakeContributor("other-plugin",
            new HostPublicHttpRouteRegistration("GET", "form/{id}", new NoOpHandler()));

        var match = PluginPublicHttpRouteMatcher.FindMatch([contributor], "my-plugin", "GET", "form/42");

        Assert.Null(match);
    }

    [Fact]
    public void RouteMatcher_TemplateMismatch_ReturnsNull()
    {
        var contributor = MakeContributor("my-plugin",
            new HostPublicHttpRouteRegistration("GET", "a/b/c", new NoOpHandler()));

        var match = PluginPublicHttpRouteMatcher.FindMatch([contributor], "my-plugin", "GET", "a/b");

        Assert.Null(match);
    }

    [Fact]
    public void RouteMatcher_NoContributors_ReturnsNull()
    {
        var match = PluginPublicHttpRouteMatcher.FindMatch([], "any-plugin", "GET", "any/path");

        Assert.Null(match);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static PublicHttpContributor MakeContributor(
        string pluginId,
        params HostPublicHttpRouteRegistration[] routes) =>
        new()
        {
            PluginId = pluginId,
            PublicHttpRoutes = routes
        };

    private static async Task<WebApplication> CreateAppAsync()
    {
        var contributor = new PublicHttpContributor
        {
            PluginId = "forms-plugin",
            PublicHttpRoutes =
            [
                // GET join/{token} → returns HTML with the token
                new HostPublicHttpRouteRegistration(
                    "GET",
                    "join/{invitationToken}",
                    new EchoTokenHandler()),

                // POST submit/{formId} → echoes formId, query "ref", body
                new HostPublicHttpRouteRegistration(
                    "POST",
                    "submit/{formId}",
                    new EchoSubmitHandler()),

                // GET go/{destination} → redirect
                new HostPublicHttpRouteRegistration(
                    "GET",
                    "go/{destination}",
                    new RedirectHandler()),

                // GET boom → throws
                new HostPublicHttpRouteRegistration(
                    "GET",
                    "boom",
                    new ThrowingHandler()),

                // GET headers → echoes the forwarded header names/values
                new HostPublicHttpRouteRegistration(
                    "GET",
                    "headers",
                    new EchoHeadersHandler()),
            ]
        };

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ICalloraPluginCatalog>(new StaticPluginCatalog(
            new Dictionary<Type, IReadOnlyList<object>>
            {
                [typeof(IHostPublicHttpEndpointContributor)] = [contributor]
            }));

        // Add a logger factory so the error-handling path can log.
        builder.Services.AddLogging();

        var app = builder.Build();
        app.MapPluginPublicHttpEndpoints();
        await app.StartAsync();
        return app;
    }
}

// ---------------------------------------------------------------------------
// Test contributor / handlers
// ---------------------------------------------------------------------------

internal sealed class PublicHttpContributor : IHostPublicHttpEndpointContributor
{
    public required string PluginId { get; init; }

    public required IReadOnlyList<HostPublicHttpRouteRegistration> PublicHttpRoutes { get; init; }
}

/// <summary>Returns HTML containing the resolved {invitationToken} route value.</summary>
internal sealed class EchoTokenHandler : IHostPublicHttpRouteHandler
{
    public ValueTask<HostPublicHttpResponse> HandleAsync(
        HostPublicHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = request.RouteValues.TryGetValue("invitationToken", out var v) ? v : "(none)";
        return ValueTask.FromResult(HostPublicHttpResponse.Html($"<p>Join: {token}</p>"));
    }
}

/// <summary>
/// Returns a plain-text response echoing the formId route value, the "ref" query
/// param, and the raw body — all three channels in one string.
/// </summary>
internal sealed class EchoSubmitHandler : IHostPublicHttpRouteHandler
{
    public ValueTask<HostPublicHttpResponse> HandleAsync(
        HostPublicHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var formId = request.RouteValues.TryGetValue("formId", out var v) ? v : "(none)";
        var refParam = request.Query.TryGetValue("ref", out var q) ? q : "(none)";
        var body = request.Body ?? "(no body)";

        return ValueTask.FromResult(new HostPublicHttpResponse(
            StatusCode: 200,
            ContentType: "text/plain",
            Body: $"formId={formId} ref={refParam} body={body}"));
    }
}

/// <summary>Redirects to /success when destination is "to-success".</summary>
internal sealed class RedirectHandler : IHostPublicHttpRouteHandler
{
    public ValueTask<HostPublicHttpResponse> HandleAsync(
        HostPublicHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(HostPublicHttpResponse.Redirect("/success"));
    }
}

/// <summary>Always throws — exercises the host's exception→500 guard.</summary>
internal sealed class ThrowingHandler : IHostPublicHttpRouteHandler
{
    public ValueTask<HostPublicHttpResponse> HandleAsync(
        HostPublicHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Handler exploded intentionally in test.");
    }
}

/// <summary>Returns a 200 OK — used as a placeholder in matcher unit tests.</summary>
internal sealed class NoOpHandler : IHostPublicHttpRouteHandler
{
    public ValueTask<HostPublicHttpResponse> HandleAsync(
        HostPublicHttpRequest request,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(HostPublicHttpResponse.Html("<p>ok</p>"));
}

/// <summary>Echoes every forwarded header as "Name=Value" lines — proves the allowlist.</summary>
internal sealed class EchoHeadersHandler : IHostPublicHttpRouteHandler
{
    public ValueTask<HostPublicHttpResponse> HandleAsync(
        HostPublicHttpRequest request,
        CancellationToken cancellationToken = default)
    {
        var lines = string.Join("\n", request.Headers.Select(h => $"{h.Key}={h.Value}"));
        return ValueTask.FromResult(new HostPublicHttpResponse(
            StatusCode: 200,
            ContentType: "text/plain",
            Body: lines));
    }
}

/// <summary>
/// A read-only stream of a fixed byte count that never sets a length, used to drive
/// a chunked (no-ContentLength) request body larger than the endpoint's cap.
/// </summary>
internal sealed class EndlessChunkStream : Stream
{
    private long _remaining;

    public EndlessChunkStream(long totalBytes) => _remaining = totalBytes;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_remaining <= 0)
        {
            return 0;
        }

        var toWrite = (int)Math.Min(count, _remaining);
        Array.Fill(buffer, (byte)'x', offset, toWrite);
        _remaining -= toWrite;
        return toWrite;
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
