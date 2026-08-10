using Callora.Core.Tests.Cli;

namespace Callora.Core.Tests.Support;

/// <summary>
/// Pfade zu den mitgebauten Test-Plugins. Sie liegen als echte Assemblies vor, damit Tests den
/// Ladepfad durch einen echten <c>PluginAssemblyLoadContext</c> nehmen können statt ihn
/// nachzubilden — die Fragen, um die es dabei geht (Typidentität, Entladbarkeit), lassen sich
/// an einer Attrappe gar nicht stellen.
/// </summary>
internal static class TestPluginAssemblies
{
    /// <summary>Das Plugin, das einen Vertrag über <c>context.Export</c> anbietet.</summary>
    internal static string Exporting() => Resolve("ExportingPlugin", "Callora.TestPlugin.Exporting.dll");

    private static string Resolve(string projectDirectory, string assemblyFileName)
    {
        // Das Plugin baut nach bin/<Config>/<Tfm>/ wie diese Testassembly auch — Konfiguration
        // und Zielframework werden deshalb aus dem eigenen Ausgabepfad übernommen statt geraten.
        var testOutput = new DirectoryInfo(
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var targetFramework = testOutput.Name;
        var configuration = testOutput.Parent!.Name;

        return Path.Combine(
            ScaffoldedPluginFixture.ResolveRepositoryRoot(),
            "tests", "TestPlugins", projectDirectory,
            "bin", configuration, targetFramework,
            assemblyFileName);
    }
}
