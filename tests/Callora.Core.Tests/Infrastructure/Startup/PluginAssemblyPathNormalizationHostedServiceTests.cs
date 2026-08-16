using Callora.Core.Application.Options;
using Callora.Core.Application.Persistence;
using Callora.Core.Application.Plugins;
using Callora.Core.Domain.Plugins;
using Callora.Core.Infrastructure.Persistence;
using Callora.Core.Infrastructure.Plugins;
using Callora.Core.Infrastructure.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Callora.Core.Tests.Infrastructure.Startup;

/// <summary>
/// Der Bestand aus der Betriebsnacht 13./14.08.: Zeilen, die auf eine Umgebung zeigen, die es hier
/// nicht gibt. Repariert wurde das von Hand mit einem <c>UPDATE … replace(…)</c> (#307).
/// </summary>
public sealed class PluginAssemblyPathNormalizationHostedServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"callora-normalize-{Guid.NewGuid():N}");

    private string PluginRoot => Path.Combine(_root, "app", "custom", "plugins");

    [Fact]
    public async Task AnAbsolutePathFromAnotherEnvironment_IsRewrittenToTheFileThatExistsHere()
    {
        var here = CreatePluginFile("videoconference", "Callora.Plugin.VideoConference.dll");
        // So stand es in der Datenbank: installiert außerhalb Docker, gestartet im Container.
        var there = Path.Combine(
            _root, "home", "dev", "Callora-Production", "custom", "plugins",
            "videoconference", "Callora.Plugin.VideoConference.dll");

        var installation = await NormalizeAsync(Installed("videoconference", there));

        Assert.Equal("${PluginDirectory}/videoconference/Callora.Plugin.VideoConference.dll", installation.StoredAssemblyPath);
        Assert.Equal(here, installation.AssemblyPath);
    }

    [Fact]
    public async Task AnAbsolutePathUnderTheCurrentRoot_BecomesRelativeToIt()
    {
        var here = CreatePluginFile("voip", "Callora.Plugin.Voip.dll");

        var installation = await NormalizeAsync(Installed("voip", here));

        Assert.Equal("${PluginDirectory}/voip/Callora.Plugin.Voip.dll", installation.StoredAssemblyPath);
    }

    // Ein Pfad, den niemand auflösen kann, ist ein Befund. Würde hier geraten, zeigte die Zeile
    // danach auf irgendetwas — und der eigentliche Fehler wäre verdeckt.
    [Fact]
    public async Task APathThatExistsNowhere_IsLeftAlone()
    {
        var missing = Path.Combine(_root, "home", "dev", "custom", "plugins", "ghost", "Callora.Plugin.Ghost.dll");

        var installation = await NormalizeAsync(Installed("ghost", missing));

        Assert.Equal(missing, installation.StoredAssemblyPath);
    }

    // Ein deinstalliertes Plugin bleibt in der Tabelle stehen (Zustandswechsel, keine Löschung).
    // Für das gibt es nichts zu heilen.
    [Fact]
    public async Task AnUninstalledRow_IsNotTouched()
    {
        CreatePluginFile("videoconference", "Callora.Plugin.VideoConference.dll");
        var there = Path.Combine(
            _root, "home", "dev", "custom", "plugins", "videoconference", "Callora.Plugin.VideoConference.dll");
        var uninstalled = Installed("videoconference", there);
        uninstalled.MarkUninstalled(DateTimeOffset.UtcNow);

        var installation = await NormalizeAsync(uninstalled);

        Assert.Equal(there, installation.StoredAssemblyPath);
    }

    private async Task<PluginInstallation> NormalizeAsync(PluginInstallation seed)
    {
        var database = $"normalize-{Guid.NewGuid()}";
        await using (var context = CreateContext(database))
        {
            await new EfPluginInstallationRepository(context, Portability()).AddAsync(seed);
            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext(database))
        {
            var services = new ServiceCollection();
            services.AddScoped<IPluginInstallationRepository>(_ => new EfPluginInstallationRepository(context, Portability()));
            services.AddScoped<IPluginAssemblyPathPortability>(_ => Portability());
            services.AddScoped<IHostUnitOfWork>(_ => new EfHostUnitOfWork(context));
            await using var provider = services.BuildServiceProvider();

            var sut = new PluginAssemblyPathNormalizationHostedService(
                provider,
                NullLogger<PluginAssemblyPathNormalizationHostedService>.Instance);
            await sut.StartAsync(CancellationToken.None);
        }

        await using var readBack = CreateContext(database);
        return (await new EfPluginInstallationRepository(readBack, Portability()).ListAsync())[0];
    }

    private static PluginInstallation Installed(string pluginId, string storedAssemblyPath)
        => PluginInstallation.CreateInstalled(pluginId, pluginId, storedAssemblyPath, null, DateTimeOffset.UtcNow);

    private string CreatePluginFile(string pluginId, string assemblyFileName)
    {
        var directory = Path.Combine(PluginRoot, pluginId);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, assemblyFileName);
        File.WriteAllText(path, "binary");
        return Path.GetFullPath(path);
    }

    private PluginAssemblyPathPortability Portability()
        => new(new CalloraHostingOptions
        {
            PluginDirectory = PluginRoot,
            StaticPluginDirectory = Path.Combine(_root, "app", "custom", "static-plugins"),
        });

    private static HostPersistenceDbContext CreateContext(string databaseName)
        => new(new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
