using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Callora.Core.Infrastructure.DependencyInjection;
using Callora.Core.Infrastructure.Mcp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Client;
using Xunit;

namespace Callora.Core.Tests.Mcp;

/// <summary>
/// Der Weg von der Plugin-Aktivierung bis zur ausgelieferten Werkzeugliste — über HTTP, mit dem
/// echten MCP-Client gegen den echten Transport (#274).
/// </summary>
/// <remarks>
/// Bis hierher prüfte nichts diesen Weg. Die Registry hat Tests, der Wrapper hat Tests, die
/// Challenge hat einen — aber ob ein aktiviertes Plugin bei einem Client ankommt, hing an der
/// Annahme, dass die Sammlung, die der Server ausliefert, dieselbe ist, die die Registry mutiert.
/// Genau diese Annahme trägt ein SDK-Major, und genau sie war beim Sprung auf
/// ModelContextProtocol 2.2.0 unbelegt.
///
/// <para>
/// Der Aufbau kommt ohne Datenbank aus: Operator-JWT plus MCP-Komposition, sonst nichts. Was hier
/// scheitert, scheitert am Protokollweg und nicht an einer Umgebung.
/// </para>
/// </remarks>
public sealed class McpToolListOverHttpTests
{
    private const string Resource = "https://callora.test/mcp";
    private const string SigningKey = "test-signing-key-that-is-long-enough-32b";

    [Fact]
    public async Task ActivatingAPlugin_MakesItsToolAppearInTheListAClientReceives()
    {
        await using var app = await StartAsync();
        var registry = app.Services.GetRequiredService<McpToolRegistry>();

        // Vorher: Der Server steht, aber kein Plugin hat etwas beigesteuert.
        await using (var client = await ConnectAsync(app))
        {
            Assert.Empty(await client.ListToolsAsync());
        }

        registry.Register("communication", new FakeMcpToolContributor(
            FakeMcpToolContributor.Tool("communication.dial")));

        await using (var client = await ConnectAsync(app))
        {
            var tool = Assert.Single(await client.ListToolsAsync());
            Assert.Equal("communication.dial", tool.Name);
        }
    }

    // Die andere Richtung, und die zählt genauso: Ein deaktiviertes Plugin darf seine Werkzeuge
    // nicht weiter anbieten — sonst ruft ein Client etwas auf, das es nicht mehr geben soll.
    [Fact]
    public async Task DeactivatingAPlugin_TakesItsToolOutOfTheListAgain()
    {
        await using var app = await StartAsync();
        var registry = app.Services.GetRequiredService<McpToolRegistry>();
        registry.Register("communication", new FakeMcpToolContributor(
            FakeMcpToolContributor.Tool("communication.dial")));

        registry.Deregister("communication");

        await using var client = await ConnectAsync(app);
        Assert.Empty(await client.ListToolsAsync());
    }

    // Die Zusage, die die eine geteilte Sammlung überhaupt erst begründet: Ein Plugin, das
    // während einer laufenden Verbindung aktiviert wird, ist ohne Neustart und ohne neue Sitzung
    // da.
    [Fact]
    public async Task AToolRegisteredDuringASession_IsThereOnTheNextList()
    {
        await using var app = await StartAsync();
        await using var client = await ConnectAsync(app);

        Assert.Empty(await client.ListToolsAsync());

        app.Services.GetRequiredService<McpToolRegistry>().Register(
            "communication",
            new FakeMcpToolContributor(FakeMcpToolContributor.Tool("communication.dial")));

        Assert.Single(await client.ListToolsAsync());
    }

    private static async Task<McpClient> ConnectAsync(WebApplication app)
    {
        var http = app.GetTestClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", OperatorToken());

        return await McpClient.CreateAsync(
            new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = new Uri("http://localhost/mcp"),
                    TransportMode = HttpTransportMode.StreamableHttp,
                },
                http,
                loggerFactory: null,
                ownsHttpClient: true));
    }

    private static string OperatorToken()
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            claims: [new Claim(ClaimTypes.NameIdentifier, "root")],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials));
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
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
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
