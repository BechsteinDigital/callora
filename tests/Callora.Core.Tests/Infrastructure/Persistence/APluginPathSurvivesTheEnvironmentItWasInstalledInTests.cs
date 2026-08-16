using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Options;
using Callora.Core.Application.Persistence;
using Callora.Core.Domain.Plugins;
using Callora.Core.Infrastructure.Persistence;
using Callora.Core.Infrastructure.Plugins;
using Microsoft.EntityFrameworkCore;

namespace Callora.Core.Tests.Infrastructure.Persistence;

/// <summary>
/// Der Befund der Betriebsnacht 13./14.08. (#307): Ein Plugin wird per <c>dotnet run</c>
/// installiert, der Host startet danach im Container — und die Zeile zeigt auf einen Pfad, den es
/// dort nicht gibt. Die Plugins laden nicht, die Verwaltung zeigt sie trotzdem als installiert,
/// und repariert wurde das mit einem <c>UPDATE … replace(…)</c> von Hand.
/// </summary>
public sealed class APluginPathSurvivesTheEnvironmentItWasInstalledInTests
{
    private static readonly string OutsideDocker = Path.GetFullPath(
        Path.Combine(Path.GetTempPath(), "callora-307", "home", "dev", "Callora-Production"));

    private static readonly string InsideDocker = Path.GetFullPath(
        Path.Combine(Path.GetTempPath(), "callora-307", "app"));

    [Fact]
    public async Task InstalledOutsideDocker_ReadBackInsideDocker_ResolvesToTheContainerPath()
    {
        var database = $"plugin-path-{Guid.NewGuid()}";

        await using (var context = CreateContext(database))
        {
            await RecordInstallationAsync(
                context,
                OutsideDocker,
                Path.Combine(OutsideDocker, "custom", "plugins", "videoconference", "Callora.Plugin.VideoConference.dll"));
        }

        await using (var context = CreateContext(database))
        {
            var installation = Assert.Single(await CreateRepository(context, InsideDocker).ListAsync());

            // Was gespeichert wurde, kennt die Umgebung nicht mehr …
            Assert.Equal(
                "${PluginDirectory}/videoconference/Callora.Plugin.VideoConference.dll",
                installation.StoredAssemblyPath);
            // … und was gelesen wird, ist der Pfad dieses Prozesses.
            Assert.Equal(
                Path.Combine(InsideDocker, "custom", "plugins", "videoconference", "Callora.Plugin.VideoConference.dll"),
                installation.AssemblyPath);
        }
    }

    /// <summary>
    /// Die Gegenprobe zum Rückweg: Ein Zustandswechsel schreibt die Zeile, und dabei darf der
    /// aufgelöste Pfad nicht in die Datenbank zurücklaufen — sonst wäre der Datenbestand nach dem
    /// ersten Aktivieren wieder an die Umgebung gebunden, in der das passierte.
    /// </summary>
    [Fact]
    public async Task ActivatingAfterAReadDoesNotWriteTheResolvedPathBack()
    {
        var database = $"plugin-path-{Guid.NewGuid()}";

        await using (var context = CreateContext(database))
        {
            await RecordInstallationAsync(
                context,
                OutsideDocker,
                Path.Combine(OutsideDocker, "custom", "plugins", "videoconference", "Callora.Plugin.VideoConference.dll"));
        }

        await using (var context = CreateContext(database))
        {
            var installation = await CreateRepository(context, InsideDocker).GetByPluginIdAsync("videoconference");
            installation!.MarkActivated(DateTimeOffset.UtcNow);
            await context.SaveChangesAsync();
        }

        await using (var readBack = CreateContext(database))
        {
            var installation = Assert.Single(await CreateRepository(readBack, InsideDocker).ListAsync());

            Assert.Equal(PluginInstallationState.Active, installation.State);
            Assert.Equal(
                "${PluginDirectory}/videoconference/Callora.Plugin.VideoConference.dll",
                installation.StoredAssemblyPath);
        }
    }

    /// <summary>
    /// Ein per NuGet oder von Hand installiertes Plugin liegt unter keiner Wurzel. Für das gibt es
    /// keine Bezugsgröße, und der absolute Pfad bleibt die richtige Antwort.
    /// </summary>
    [Fact]
    public async Task InstalledOutsideBothRoots_KeepsItsAbsolutePath()
    {
        var database = $"plugin-path-{Guid.NewGuid()}";
        var foreign = Path.Combine(Path.GetTempPath(), "callora-307", "opt", "vendor", "Callora.Plugin.Foreign.dll");

        await using (var context = CreateContext(database))
        {
            await RecordInstallationAsync(context, OutsideDocker, foreign, pluginId: "foreign");
        }

        await using (var context = CreateContext(database))
        {
            var installation = Assert.Single(await CreateRepository(context, InsideDocker).ListAsync());

            Assert.Equal(foreign, installation.StoredAssemblyPath);
            Assert.Equal(foreign, installation.AssemblyPath);
        }
    }

    private static async Task RecordInstallationAsync(
        HostPersistenceDbContext context,
        string root,
        string assemblyPath,
        string pluginId = "videoconference")
    {
        var recorder = new PluginInstallationRecorder(
            CreateRepository(context, root),
            new EfHostUnitOfWork(context),
            Portability(root));

        await recorder.RecordInstalledAsync(pluginId, pluginId, assemblyPath, entryTypeName: null, CancellationToken.None);
    }

    private static IPluginInstallationRepository CreateRepository(HostPersistenceDbContext context, string root)
        => new EfPluginInstallationRepository(context, Portability(root));

    private static PluginAssemblyPathPortability Portability(string root)
        => new(new CalloraHostingOptions
        {
            PluginDirectory = Path.Combine(root, "custom", "plugins"),
            StaticPluginDirectory = Path.Combine(root, "custom", "static-plugins"),
        });

    private static HostPersistenceDbContext CreateContext(string databaseName)
        => new(new DbContextOptionsBuilder<HostPersistenceDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);
}
