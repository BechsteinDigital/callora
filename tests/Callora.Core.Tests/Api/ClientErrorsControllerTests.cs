using Callora.Core.Api;
using Callora.Core.Application.Monitoring;
using Callora.Core.Application.Policies;
using Callora.Core.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

namespace Callora.Core.Tests.Api;

/// <summary>
/// Die Senke aus #294 nimmt Meldungen von jedem entgegen — eine Surface-Seite hat keinen
/// angemeldeten Besucher. Geprüft wird deshalb nicht nur, dass eine Meldung ankommt, sondern auch,
/// was mit ihr passiert, bevor sie im Log steht.
/// </summary>
public sealed class ClientErrorsControllerTests
{
    [Fact]
    public async Task Report_FromAnAnonymousBrowser_IsAcceptedAndLogged()
    {
        var logs = new RecordingLoggerFactory();
        await using var app = await CreateAppAsync(logs);

        var response = await app.GetTestClient().PostAsJsonAsync(
            "/api/client-errors",
            new ClientErrorReport("surface", "Cannot read properties of undefined", "at mount (app.js:1:2)", "/portal/termin"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var (level, message) = Assert.Single(logs.Entries);
        // Warnung, nicht Information: Der Betrieb soll davon erfahren, ohne danach zu suchen.
        Assert.Equal(LogLevel.Warning, level);
        Assert.Contains("Cannot read properties of undefined", message, StringComparison.Ordinal);
    }

    // Die Antwort ist leer und immer dieselbe: Wer meldet, bekommt nichts über das System zurück
    // — und schon gar nicht seine eigene Eingabe.
    [Fact]
    public async Task Report_IsAnsweredWithoutABody()
    {
        await using var app = await CreateAppAsync(new RecordingLoggerFactory());

        var response = await app.GetTestClient().PostAsJsonAsync(
            "/api/client-errors",
            new ClientErrorReport("surface", "<script>alert(1)</script>", null, null));

        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Report_WithAQueryInTheUrl_LogsThePathWithoutIt()
    {
        var logs = new RecordingLoggerFactory();
        await using var app = await CreateAppAsync(logs);

        await app.GetTestClient().PostAsJsonAsync(
            "/api/client-errors",
            new ClientErrorReport("surface", "boom", null, "/portal/termin?email=anna%40example.org"));

        var (_, message) = Assert.Single(logs.Entries);
        Assert.Contains("/portal/termin", message, StringComparison.Ordinal);
        Assert.DoesNotContain("anna", message, StringComparison.Ordinal);
    }

    // Ohne Begrenzung wäre die Senke ein offenes Logziel: Eine Seite, die in einer Schleife
    // scheitert, meldet mit jeder Runde, und ein Absender mit Absicht tut dasselbe schneller.
    [Fact]
    public async Task Report_BeyondTheClientWindow_IsRefusedWith429()
    {
        await using var app = await CreateAppAsync(new RecordingLoggerFactory(), reportsPerMinute: 2);
        var client = app.GetTestClient();
        var report = new ClientErrorReport("surface", "boom", null, null);

        await client.PostAsJsonAsync("/api/client-errors", report);
        await client.PostAsJsonAsync("/api/client-errors", report);
        var third = await client.PostAsJsonAsync("/api/client-errors", report);

        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    private static async Task<WebApplication> CreateAppAsync(
        RecordingLoggerFactory logs,
        int reportsPerMinute = 100)
    {
        var options = new BackendHostOptions { RateLimitClientErrorsPerMinute = reportsPerMinute };

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ILoggerFactory>(logs);
        builder.Services.AddSingleton(options);
        builder.Services.AddAuthorization();
        builder.Services.AddBackendRateLimiting(options);
        // Über MVC, weil der Endpunkt ein Controller ist: Die Architekturregel des Hauses
        // (ArchitectureRulesTests.MinimalApiEndpoints_StayWithinTheMigrationBaseline) lässt neue
        // Minimal-API-Endpunkte nicht zu, und die Baseline schrumpft nur.
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(ClientErrorsController).Assembly);

        var app = builder.Build();
        app.UseRouting();
        app.UseRateLimiter();
        app.MapControllers();
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// Hält Stufe UND Text fest — die gemeinsame Hilfe im Support erfasst nur den Text.
    /// <para>
    /// Aufgezeichnet wird ausschließlich die eigene Kategorie der Senke. Das hält nicht nur das
    /// Rauschen des Frameworks heraus: Die Kategorie ist eine Zusage an den Betrieb, der danach
    /// filtert, und ein Test, der jede Kategorie nimmt, würde ihren Verlust nicht bemerken.
    /// </para>
    /// </summary>
    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

        public IReadOnlyList<(LogLevel Level, string Message)> Entries => [.. _entries];

        public ILogger CreateLogger(string categoryName)
            => categoryName == ClientErrorsController.LogCategory
                ? new RecordingLogger(_entries)
                : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public void Dispose()
        {
        }

        private sealed class RecordingLogger(ConcurrentQueue<(LogLevel, string)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
                => entries.Enqueue((logLevel, formatter(state, exception)));
        }
    }
}
