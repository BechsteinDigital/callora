using Callora.Core.Application.Persistence.Contracts;
using Callora.Core.Application.Plugins.Contracts;
using Callora.Core.Domain.Plugins.Contracts;
using Microsoft.Extensions.Logging;

namespace Callora.TestPlugin.Exporting;

/// <summary>
/// Minimal host-managed plugin used only by the RuntimePluginHost activation
/// harness. It exports one contract in <see cref="StartAsync"/> so the test can
/// prove the whole runtime chain: a plugin's <c>context.Export</c> is resolvable
/// by the host across the plugin ALC (unified <c>Callora.*</c> contract type),
/// and the export is withdrawn on deactivation.
/// </summary>
public sealed class ExportingTestPlugin : IHostManagedPlugin
{
    public string PluginId => "exporting-test-plugin";

    public string DisplayName => "Exporting Test Plugin";

    /// <summary>Der aufgelöste Logger — als beobachtbare Zuweisung, damit der JIT sie nicht wegoptimiert.</summary>
    public static ILogger? Logger { get; private set; }

    public ValueTask StartAsync(IHostPluginContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Wie ein echtes Plugin: ILogger<EigenerTyp> aus den Diensten. Genau dieser Weg trägt den
        // geschlossenen generischen Plugin-Typ in den Wurzel-Container des Hosts — und hielt damit
        // den Ladekontext fest. Ohne diese Zeile prüft der Entlade-Test einen Fall, den es in
        // Produktion nicht gibt.
        Logger = context.Services.GetService(typeof(ILogger<ExportingTestPlugin>)) as ILogger;

        context.Export<IWorkspaceDataPurgeContributor>(new NoopPurgeContributor());
        context.Export<IRuntimeCapabilitySource>(new StaticRuntimeCapabilitySource());
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

    private sealed class NoopPurgeContributor : IWorkspaceDataPurgeContributor
    {
        public Task PurgeWorkspaceAsync(string workspaceKey, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    // A fixed runtime-capability grant so the host activation test can prove the source-registration
    // wiring (activation registers it into the RuntimeCapabilityRegistry; deactivation unregisters it).
    private sealed class StaticRuntimeCapabilitySource : IRuntimeCapabilitySource
    {
        public IReadOnlyCollection<RuntimeCapabilityGrant> CurrentGrants { get; } =
            [new RuntimeCapabilityGrant("exporting-test.capability", "ws-test")];

#pragma warning disable CS0067 // The test source never raises changes.
        public event Action<RuntimeCapabilityChanged>? CapabilitiesChanged;
#pragma warning restore CS0067
    }
}
