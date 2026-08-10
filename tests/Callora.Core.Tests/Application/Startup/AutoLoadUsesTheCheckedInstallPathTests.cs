using Callora.Core.Application.Lifecycle;
using Callora.Core.Application.Options;
using Callora.Core.Application.Plugins;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Application.Startup;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Callora.Core.Tests.Application.Startup;

/// <summary>
/// Auto-Load beim Start geht durch dieselben Tore wie jede andere Installation.
/// </summary>
/// <remarks>
/// <para>
/// Der Befund: Die Schleife rief <c>ICalloraPluginRuntime.InstallAsync</c> direkt auf und
/// umging damit den <see cref="IPluginLifecycleService"/> — und mit ihm die Signaturprüfung,
/// den Registry-Abgleich und den Audit-Eintrag. Wer eine DLL ins Plugin-Verzeichnis legen
/// konnte, brachte sie am gesamten Vertrauensmodell vorbei in den Prozess.
/// </para>
/// <para>
/// Verschärfend: Dieser Dienst läuft VOR der geprüften Discovery. Der ungeprüfte Pfad war
/// also nicht die Ausnahme, sondern der Normalfall — die Discovery fand danach nur noch
/// bereits Installiertes vor.
/// </para>
/// </remarks>
public sealed class AutoLoadUsesTheCheckedInstallPathTests
{
    [Fact]
    public async Task AutoLoad_InstallsThroughTheLifecycleService()
    {
        using var directory = new TempPluginDirectory("acme.plugin.dll");
        var lifecycle = new RecordingLifecycleService();
        var services = new ServiceCollection()
            .AddSingleton<IPluginLifecycleService>(lifecycle)
            .BuildServiceProvider();

        // Die Runtime wirft bei InstallAsync: Wird sie doch aufgerufen, scheitert der Test —
        // genau so war der Zustand vor dem Fix.
        var runtime = new ThrowingOnInstallRuntime();
        var options = new CalloraHostingOptions
        {
            AutoLoadPlugins = true,
            PluginDirectory = directory.Path,
            AutoActivateInstalledPlugins = false,
        };

        await new CalloraHostStartup(options, runtime).StartAsync(services);

        var installed = Assert.Single(lifecycle.Installed);
        Assert.Equal(directory.AssemblyPath, installed.AssemblyPath);
        Assert.Equal("system:startup-autoload", installed.RequestedBy);
    }

    [Fact]
    public async Task WithoutALifecycleService_NothingIsInstalled()
    {
        // Fail-closed: Ohne die Tore wird nicht installiert. Der stille Rückfall auf die rohe
        // Runtime wäre genau die Lücke, die dieser Fix schließt — eine Komposition ohne
        // Lifecycle-Service darf kein Schlupfloch sein.
        using var directory = new TempPluginDirectory("acme.plugin.dll");
        var services = new ServiceCollection().BuildServiceProvider();
        var runtime = new ThrowingOnInstallRuntime();
        var options = new CalloraHostingOptions
        {
            AutoLoadPlugins = true,
            PluginDirectory = directory.Path,
            AutoActivateInstalledPlugins = false,
        };

        await new CalloraHostStartup(options, runtime).StartAsync(services);

        Assert.False(runtime.InstallWasCalled);
    }

    private sealed class TempPluginDirectory : IDisposable
    {
        public TempPluginDirectory(string assemblyFileName)
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "callora-autoload-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            AssemblyPath = System.IO.Path.Combine(Path, assemblyFileName);
            File.WriteAllText(AssemblyPath, "nicht wirklich eine Assembly");
        }

        public string Path { get; }

        public string AssemblyPath { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private sealed class RecordingLifecycleService : IPluginLifecycleService
    {
        public List<InstallPluginCommand> Installed { get; } = [];

        public IReadOnlyCollection<HostPluginDescriptor> Plugins => [];

        public Task<PluginLifecycleServiceResult> InstallAsync(
            InstallPluginCommand command,
            CancellationToken cancellationToken = default)
        {
            Installed.Add(command);
            return Task.FromResult(new PluginLifecycleServiceResult(
                PluginLifecycleServiceStatus.Ok, true, null, "ok", null));
        }

        public Task<IReadOnlyList<PluginInstallationSnapshot>> GetInstallationsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<PluginInstallationSnapshot>>([]);

        public Task<PluginLifecycleServiceResult> InstallFromNuGetAsync(InstallNuGetPluginCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PluginLifecycleServiceResult> UpdateFromNuGetAsync(UpdateNuGetPluginCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PluginLifecycleServiceResult> UpdateFromLocalAsync(UpdateLocalPluginCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PluginLifecycleServiceResult> ActivateAsync(PluginLifecycleCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PluginLifecycleServiceResult> DeactivateAsync(PluginLifecycleCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PluginLifecycleServiceResult> UninstallAsync(PluginLifecycleCommand command, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingOnInstallRuntime : ICalloraPluginRuntime
    {
        public bool InstallWasCalled { get; private set; }

        public IReadOnlyCollection<RuntimePluginDescriptor> LoadedPlugins => [];

        public Task<RuntimePluginInstallResult> InstallAsync(string assemblyPath, string? entryTypeName = null, CancellationToken cancellationToken = default)
        {
            InstallWasCalled = true;
            throw new InvalidOperationException(
                "Auto-Load darf die Runtime nicht direkt installieren lassen — das umgeht Signatur und Registry.");
        }

        public Task<RuntimePluginActivateResult> ActivateAsync(string pluginId, CancellationToken cancellationToken = default)
            => Task.FromResult(new RuntimePluginActivateResult(RuntimePluginActivateStatus.Activated, pluginId));

        public Task<RuntimePluginDeactivateResult> DeactivateAsync(string pluginId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RuntimePluginUninstallResult> UninstallAsync(string pluginId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public bool TryGetExport(Type contractType, out object? service)
        {
            service = null;
            return false;
        }

        public IReadOnlyList<object> GetExports(Type contractType) => [];

        public IReadOnlyList<CalloraPluginExport> GetOwnedExports(Type contractType) => [];
    }
}
