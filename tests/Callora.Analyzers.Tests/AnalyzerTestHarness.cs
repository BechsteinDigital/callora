using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Callora.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Callora.Analyzers.Tests;

/// <summary>
/// Runs <see cref="CalloraInternalConsumptionAnalyzer"/> against in-memory compilations.
/// Hand-rolled on top of Microsoft.CodeAnalysis rather than the churny analyzer-testing
/// packages, so the harness is dependency-stable.
/// </summary>
internal static class AnalyzerTestHarness
{
    private const string FrameworkProperty = "build_property.CalloraFrameworkAssembly";

    private static readonly ImmutableArray<MetadataReference> RuntimeReferences = LoadRuntimeReferences();

    /// <summary>Compiles a stand-in "framework" assembly and returns it as a reference.</summary>
    public static MetadataReference CompileReference(string source, string assemblyName)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { Parse(source) },
            RuntimeReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        return compilation.ToMetadataReference();
    }

    /// <summary>Runs the analyzer over <paramref name="source"/> and returns the CAL0001 diagnostics.</summary>
    public static async Task<ImmutableArray<Diagnostic>> RunAsync(
        string source,
        bool frameworkAssembly,
        params MetadataReference[] extraReferences)
    {
        var references = RuntimeReferences.AddRange(extraReferences);
        var compilation = CSharpCompilation.Create(
            "Consumer",
            new[] { Parse(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (frameworkAssembly)
        {
            values[FrameworkProperty] = "true";
        }

        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(new CalloraInternalConsumptionAnalyzer()),
            new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, new OptionsProvider(values)));

        var diagnostics = await withAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
        return diagnostics
            .Where(d => d.Id == CalloraInternalConsumptionAnalyzer.DiagnosticId)
            .ToImmutableArray();
    }

    private static SyntaxTree Parse(string source)
        => CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest));

    private static ImmutableArray<MetadataReference> LoadRuntimeReferences()
    {
        var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        return trusted
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToImmutableArray();
    }

    private sealed class OptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly Options _options;

        public OptionsProvider(IReadOnlyDictionary<string, string> values) => _options = new Options(values);

        public override AnalyzerConfigOptions GlobalOptions => _options;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
    }

    private sealed class Options : AnalyzerConfigOptions
    {
        private readonly IReadOnlyDictionary<string, string> _values;

        public Options(IReadOnlyDictionary<string, string> values) => _values = values;

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string value)
        {
            if (_values.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }

            value = null!;
            return false;
        }
    }
}
