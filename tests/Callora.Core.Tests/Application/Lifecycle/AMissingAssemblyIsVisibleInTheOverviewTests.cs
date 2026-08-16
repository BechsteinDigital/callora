using Callora.Core.Application.Entitlements;
using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Policies;
using Callora.Core.Domain.Plugins;
using Callora.Core.Tests.Support;

namespace Callora.Core.Tests.Application.Lifecycle;

/// <summary>
/// Die Sofortmaßnahme aus #307: Ein installiertes Plugin, dessen Assembly fehlt, soll in der
/// Verwaltung als solches erscheinen. Bisher stand der Befund in einer Warnung beim Start, zwischen
/// hunderten Zeilen EF-SQL — sichtbar wurde er als fehlende Oberfläche, und die Übersicht zeigte
/// das Plugin unverändert als installiert.
/// </summary>
public sealed class AMissingAssemblyIsVisibleInTheOverviewTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"callora-missing-{Guid.NewGuid():N}");

    [Fact]
    public async Task AnInstallationWhoseFileIsGone_IsReportedAsMissing()
    {
        var installations = new InMemoryPluginInstallationRepository();
        await installations.AddAsync(PluginInstallation.CreateInstalled(
            "videoconference",
            "Videoconference",
            Path.Combine(_directory, "videoconference", "Callora.Plugin.VideoConference.dll"),
            entryTypeName: null,
            DateTimeOffset.UtcNow));

        var snapshot = Assert.Single(await CreateSut(installations).GetInstallationsAsync());

        Assert.True(snapshot.AssemblyMissing);
    }

    [Fact]
    public async Task AnInstallationWhoseFileIsThere_IsNotReportedAsMissing()
    {
        Directory.CreateDirectory(_directory);
        var assemblyPath = Path.Combine(_directory, "Callora.Plugin.VideoConference.dll");
        await File.WriteAllTextAsync(assemblyPath, "binary");

        var installations = new InMemoryPluginInstallationRepository();
        await installations.AddAsync(PluginInstallation.CreateInstalled(
            "videoconference",
            "Videoconference",
            assemblyPath,
            entryTypeName: null,
            DateTimeOffset.UtcNow));

        var snapshot = Assert.Single(await CreateSut(installations).GetInstallationsAsync());

        Assert.False(snapshot.AssemblyMissing);
    }

    private static PluginLifecycleService CreateSut(InMemoryPluginInstallationRepository installations)
        => new(
            new FakeHostPluginLifecycle(),
            new StaticPluginActivationPolicy(PluginActivationDecision.Allow()),
            new InMemoryPluginEntitlementStore(new BackendHostOptions()),
            new InMemoryHostAuditStore(),
            installations,
            new NoOpHostUnitOfWork(),
            new StaticPluginPackageRegistryReader(),
            new StaticPluginPackageSignatureVerifier(),
            new StaticNuGetPluginAssemblyResolver(),
            new RecordingHostApplicationEventPublisher());

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
