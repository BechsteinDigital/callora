using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;

namespace Callora.Analyzers;

/// <summary>
/// CAL0005 — reports consumption of a <c>[CalloraDeprecated]</c>-marked type or member from
/// outside the Callora framework assemblies. The member still works; it is on its way out.
/// </summary>
/// <remarks>
/// <para>
/// A warning, not an error, and that is the whole point: it reaches a plugin author in
/// their own build, in their own repository, at a time they choose. Before the middle rung
/// existed the first news of a removal was a plugin that would not load — at install time,
/// in someone else's deployment.
/// </para>
/// <para>
/// Framework assemblies are exempt for the same reason CAL0001 exempts them: the platform
/// implements and calls its own deprecated surface for as long as it ships it. Warning
/// there would flood the host build and teach everyone to ignore the rule before a single
/// plugin author ever saw it.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CalloraDeprecatedConsumptionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The stable diagnostic id for deprecated-API consumption.</summary>
    public const string DiagnosticId = "CAL0005";

    private const string DeprecatedAttributeMetadataName = "Callora.Core.Extensibility.CalloraDeprecatedAttribute";
    private const string FrameworkAssemblyProperty = "build_property.CalloraFrameworkAssembly";

    internal static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Using a deprecated Callora extension surface",
        messageFormat: "'{0}' is deprecated since {1} and stops working in contract version {2}{3}",
        category: "Callora.Extensibility",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Members marked [CalloraDeprecated] still work but are announced for removal in a named contract version. Migrate before that version; the announcement is a promise that the member survives until then, and the extension-surface gate refuses an earlier removal.",
        helpLinkUri: "https://github.com/BechsteinDigital/callora/blob/main/docs-site/reference/analyzer-rules.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        if (IsFrameworkAssembly(context.Options.AnalyzerConfigOptionsProvider.GlobalOptions))
        {
            return;
        }

        var marker = context.Compilation.GetTypeByMetadataName(DeprecatedAttributeMetadataName);
        if (marker is null)
        {
            return;
        }

        var enforcement = new CalloraDeprecatedConsumptionEnforcement(marker, context.Compilation.Assembly);

        context.RegisterOperationAction(
            enforcement.AnalyzeOperation,
            OperationKind.Invocation,
            OperationKind.ObjectCreation,
            OperationKind.PropertyReference,
            OperationKind.FieldReference,
            OperationKind.EventReference,
            OperationKind.MethodReference);

        // Implementing a deprecated interface is the case that matters most: plugins
        // implement contracts far more often than they call them, so a rule watching only
        // call sites would miss the majority of what a deprecation is meant to reach.
        context.RegisterSymbolAction(enforcement.AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static bool IsFrameworkAssembly(AnalyzerConfigOptions options)
        => options.TryGetValue(FrameworkAssemblyProperty, out var value)
           && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
