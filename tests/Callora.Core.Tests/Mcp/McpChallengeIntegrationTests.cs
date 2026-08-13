using System.Net;
using System.Text;
using Callora.Core.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Callora.Core.Tests.Mcp;

/// <summary>
/// The RFC 9728 resource-server behaviour of the <c>/mcp</c> mount: an unauthenticated request is
/// answered by the MCP authentication handler (not the app-wide challenge), so its
/// <c>WWW-Authenticate</c> header carries the <c>resource_metadata</c> pointer at the protected-resource
/// metadata document. Verified end-to-end with a TestServer that wires only the operator-JWT scheme plus
/// the MCP composition — no database is needed.
/// </summary>
public sealed class McpChallengeIntegrationTests
{
    private const string Resource = "https://callora.test/mcp";

    [Fact]
    public async Task UnauthenticatedMcpRequest_Returns401_WithResourceMetadataChallenge()
    {
        await using var app = await StartAsync();

        // POST und nicht GET, seit ModelContextProtocol 2.2.0: Der Streamable-HTTP-Transport
        // reserviert GET für den SSE-Strom und beantwortet es an dieser Route mit 405, bevor die
        // Authentifizierung überhaupt zum Zug kommt. Ein Client, der die Challenge sucht, schickt
        // ohnehin einen Request — und genau der bekommt sie weiterhin. Die Zusage aus RFC 9728
        // steht also unverändert; nur die Methode, über die dieser Test sie abfragte, war eine,
        // die das Protokoll dafür nie vorgesehen hat.
        var probe = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        probe.Headers.Add("Accept", "application/json, text/event-stream");

        var response = await app.GetTestClient().SendAsync(probe);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = string.Join(" ", response.Headers.WwwAuthenticate.ToString());
        Assert.Contains("resource_metadata", challenge);
        Assert.Contains("/.well-known/oauth-protected-resource", challenge);
    }

    [Fact]
    public async Task ProtectedResourceMetadata_IsPubliclyDiscoverable()
    {
        await using var app = await StartAsync();

        var response = await app.GetTestClient().GetAsync("/.well-known/oauth-protected-resource");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(Resource, body);
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var authentication = builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes("test-signing-key-that-is-long-enough-32b"))
            });

        builder.Services.AddCalloraMcp(
            authentication,
            forwardAuthenticateScheme: JwtBearerDefaults.AuthenticationScheme,
            resource: Resource,
            authorizationServer: null);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapCalloraMcp();
        await app.StartAsync();
        return app;
    }
}
