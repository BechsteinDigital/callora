using Callora.Core.Application.Policies;
using Callora.Core.Application.Surfaces;
using Callora.Core.Application.Surfaces.SharedContext;
using Callora.Surface.Rendering;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Callora.Core.Tests.Surface;

/// <summary>
/// Der geteilte Kontext war vollständig gebaut, getestet und an keiner Stelle verdrahtet: Weder
/// ISharedContextService noch SharedContextStore waren registriert, und es gibt kein
/// Assembly-Scanning, das das nachgeholt hätte. Die Extension-Point-Doku führte beide trotzdem als
/// „resolvable" samt Beispielcode — ein Plugin, das ihr folgte, bekam eine InvalidOperationException.
/// <para>
/// Ein Test der Registrierung und nicht der Logik: Was fehlte, war nicht das Verhalten, sondern die
/// Zeile, die es erreichbar macht.
/// </para>
/// </summary>
public sealed class TheSharedContextExtensionPointIsResolvableTests
{
    [Fact]
    public async Task APluginCanResolveTheSharedContextService()
    {
        await using var app = BuildHost();

        var service = app.Services.GetService<ISharedContextService>();

        Assert.NotNull(service);
        Assert.IsType<SharedContextBroadcastService>(service);
    }

    /// <summary>
    /// Ohne Contributoren bleibt der Speicher leer — und genau das muss er tun, statt beim Bauen
    /// zu scheitern. Ein Host ohne Plugin, das Schlüssel deklariert, ist der Normalfall.
    /// </summary>
    [Fact]
    public async Task TheStoreBuildsWithoutAnyKeyContributor()
    {
        await using var app = BuildHost();

        var store = app.Services.GetService<SharedContextStore>();

        Assert.NotNull(store);
        Assert.Empty(store.DeclaredKeys);
    }

    /// <summary>
    /// Und mit einem Contributor stehen dessen Schlüssel drin: Der Extension-Point trägt, er ist
    /// nicht nur auflösbar.
    /// </summary>
    [Fact]
    public async Task ADeclaredKeyReachesTheStore()
    {
        await using var app = BuildHost(services =>
            services.AddSingleton<ISharedContextKeyContributor, TestKeyContributor>());

        var store = app.Services.GetRequiredService<SharedContextStore>();

        Assert.Contains("test.caller", store.DeclaredKeys);
        Assert.NotNull(store.Declaration("test.caller"));
    }

    private static WebApplication BuildHost(Action<IServiceCollection>? configure = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddSingleton(new BackendHostOptions());
        builder.Services.AddScoped<SurfaceSessionAuthenticator>(_ => null!);
        configure?.Invoke(builder.Services);
        builder.Services.AddCalloraSurfaceRendering();

        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateScopes = true;
            options.ValidateOnBuild = false;
        });

        return builder.Build();
    }

    private sealed class TestKeyContributor : ISharedContextKeyContributor
    {
        public IReadOnlyList<SharedContextKeyDeclaration> SharedContextKeys { get; } =
        [
            new SharedContextKeyDeclaration(
                "test.caller",
                SharedContextAnchorType.Conversation,
                "Proves the extension point is wired; carries nothing real.",
                [new SharedContextFieldDeclaration("name", SharedContextVisibility.Participant, "Caller name.")],
                TimeSpan.FromMinutes(5),
                "test-plugin")
        ];
    }
}
