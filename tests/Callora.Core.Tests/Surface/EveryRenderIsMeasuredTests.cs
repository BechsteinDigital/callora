using Callora.Core.Application.Extensions;
using Callora.Core.Application.Policies;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Workspaces;
using Callora.Core.Tests.Support;
using Callora.Surface.Rendering;
using Callora.Surface.Rendering.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Der öffentliche Renderpfad ist der einzige, den Endkunden treffen — und er war der einzige
/// ohne jede Messung. Das Runbook führte einen Abschnitt „Surface render failure / degradation",
/// der eine Diagnose beschrieb, für die es keine Daten gab.
/// <para>
/// Diese Tests halten die drei Zusicherungen fest, auf die sich ein Alarm stützen kann: Jeder
/// Render wird gezählt, ein Fehlschlag trägt seinen Grund, und ein Pfad, für den diese Fläche gar
/// nicht zuständig ist, verfälscht die Statistik nicht.
/// </para>
/// </summary>
[Collection(SurfaceRenderingCollection.Name)]
public sealed class EveryRenderIsMeasuredTests
{
    [Fact]
    public async Task ASuccessfulRender_IsCountedWithItsWorkspaceAndSurface()
    {
        using var metrics = new RenderMetricRecorder();
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        await client.GetAsync("/surface/render");

        var measurement = metrics.Only(SurfaceRenderTelemetry.RequestCountMetricName, workspace: "acme");
        Assert.Equal("success", measurement.Outcome);
        Assert.Equal("acme", measurement.Workspace);
        Assert.Equal("default", measurement.Surface);

        // Der Grund ist auch im Erfolgsfall gesetzt, damit das Tag-Schema über beide Ausgänge
        // stabil bleibt: Ein Alarm, der auf "reason" gruppiert, bekommt sonst je nach Ausgang
        // eine andere Zeitreihenform.
        Assert.Equal("none", measurement.Reason);
    }

    [Fact]
    public async Task ARenderIsAlsoTimed()
    {
        using var metrics = new RenderMetricRecorder();
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        await client.GetAsync("/surface/render");

        var measurement = metrics.Only(SurfaceRenderTelemetry.DurationMetricName, workspace: "acme");
        Assert.Equal("success", measurement.Outcome);
        Assert.True(measurement.Value >= 0, "Die Renderdauer darf nicht negativ sein.");
    }

    /// <summary>
    /// Ein Host, für den keine Fläche konfiguriert ist, ist aus Sicht des Betriebs kein
    /// Nicht-Ereignis: Entweder vertippt sich ein Besucher, oder eine Fläche ist falsch
    /// verdrahtet. Beides gehört gezählt — mit Grund, damit man die Fälle trennen kann.
    /// </summary>
    [Fact]
    public async Task AnUnresolvableRoute_IsCountedAsAFailureWithItsReason()
    {
        using var metrics = new RenderMetricRecorder();
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://niemand.example.de/");

        await client.GetAsync("/surface/render");

        var measurement = metrics.Only(
            SurfaceRenderTelemetry.RequestCountMetricName,
            reason: SurfaceRenderTelemetry.ReasonRouteNotFound);
        Assert.Equal("failure", measurement.Outcome);

        // Ohne aufgelöste Fläche gibt es keinen Workspace zu nennen. Leer statt geraten: Ein
        // erfundener Wert wäre in jedem Dashboard eine Lüge.
        Assert.Equal(string.Empty, measurement.Workspace);
    }

    /// <summary>
    /// Der Catch-All fängt auch `/api/…`, wenn dort ein Endpunkt fehlt. Diese Anfragen als
    /// Render-Fehlschläge zu zählen hieße, die Fehlerrate der Oberfläche mit vertippten
    /// API-Aufrufen zu füllen — der Alarm wäre danach unbrauchbar.
    /// </summary>
    [Fact]
    public async Task APlatformOwnedPath_IsNotCountedAsARenderAtAll()
    {
        using var metrics = new RenderMetricRecorder();
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        await client.GetAsync("/api/does-not-exist");

        Assert.Empty(metrics.All(SurfaceRenderTelemetry.RequestCountMetricName));
    }

    [Fact]
    public async Task ARender_OpensATraceSpan()
    {
        var activities = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SurfaceRenderTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);

        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        client.BaseAddress = new Uri("http://acme.example.de/");

        await client.GetAsync("/surface/render");

        var render = Assert.Single(activities, activity => activity.OperationName == "surface.render");
        Assert.Equal("acme", render.GetTagItem("workspace.key"));
        Assert.Equal("default", render.GetTagItem("surface.key"));
        Assert.Equal(ActivityStatusCode.Ok, render.Status);
    }

    private static async Task<WebApplication> CreateAppAsync()
    {
        var store = new InMemoryWorkspaceManagementStore();
        store.AddTenant("tenant-a");
        _ = await store.UpsertAsync(
            "tenant-a", "acme", "Acme", "spa", isActive: true, defaultSurfaceBaseUrl: "https://acme.example.de");

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<IWorkspaceManagementStore>(store);
        builder.Services.AddSingleton<IWorkspaceSurfaceStore>(new InMemoryWorkspaceSurfaceStore());
        builder.Services.AddSingleton(new BackendHostOptions
        {
            DefaultTenantKey = "tenant-a",
            AdminShellBaseUrl = "/admin",
            WorkspaceShellBaseUrl = "/"
        });
        builder.Services.AddSingleton<IWorkspaceTemplateResolutionService>(
            new StaticWorkspaceTemplateResolutionService([]));
        builder.Services.AddSingleton<IWorkspacePluginActivationReader>(
            new StaticWorkspacePluginActivationReader([]));
        builder.Services.AddSingleton<IPluginAvailabilityEvaluator>(
            new StaticPluginAvailabilityEvaluator());
        builder.Services.AddSingleton<IWorkspaceThemeSettingsStore>(
            new InMemoryWorkspaceThemeSettingsStore());
        builder.Services.AddScoped<WorkspaceUiChainResolver>();
        builder.Services.AddSingleton<IWorkspaceSectionLayoutStore>(
            new InMemoryWorkspaceSectionLayoutStore());
        builder.Services.AddScoped<WorkspacePublicThemeResolver>();
        // Der Port zeigt hier direkt auf den echten Resolver, nicht auf den Cache: Diese Tests
        // schreiben und lesen im selben Lauf und müssen sehen, was sie gerade gesetzt haben.
        builder.Services.AddScoped<IWorkspacePublicThemeResolver>(
            static sp => sp.GetRequiredService<WorkspacePublicThemeResolver>());
        builder.Services.AddCalloraSurfaceRendering();

        // NACH AddCalloraSurfaceRendering, damit der Stub den echten Renderer verdrängt: Diese
        // Tests prüfen den Weg durch den Endpunkt, nicht die Template-Engine — und fünf echte
        // Jint-Renders in einem Testprozess reißen sporadisch dessen Zwei-Sekunden-Grenze.
        builder.Services.AddSingleton<ISurfaceRenderer, StubSurfaceRenderer>();

        var app = builder.Build();
        app.MapSurfaceRenderEndpoints();
        await app.StartAsync();
        return app;
    }
}
