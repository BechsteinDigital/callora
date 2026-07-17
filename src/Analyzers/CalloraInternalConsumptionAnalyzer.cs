using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Callora.Analyzers;

/// <summary>
/// CAL0001 — reports consumption of a <c>[CalloraInternal]</c>-marked type or member
/// from outside the Callora framework assemblies. Such APIs are public for technical
/// reasons only and are not part of the stable plugin contract (REV2 §7.1).
/// </summary>
/// <remarks>
/// A compilation is treated as a framework assembly (and therefore exempt) when the
/// MSBuild property <c>CalloraFrameworkAssembly=true</c> is visible to the compiler.
/// Every other compilation — plugins in particular — is enforced. Consuming a marked
/// symbol declared in the same assembly is always allowed.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CalloraInternalConsumptionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The stable diagnostic id for internal-API consumption.</summary>
    public const string DiagnosticId = "CAL0001";

    private const string InternalAttributeMetadataName = "Callora.Core.Extensibility.CalloraInternalAttribute";
    private const string FrameworkAssemblyProperty = "build_property.CalloraFrameworkAssembly";

    internal static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Consuming a [CalloraInternal] API from outside the framework",
        messageFormat: "'{0}' is marked [CalloraInternal] and is not part of the Callora plugin contract; it must not be consumed outside Callora framework assemblies{1}",
        category: "Callora.Extensibility",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types and members marked [CalloraInternal] are visible for technical reasons only and are not a stable plugin contract. Plugins must extend Callora through documented extension points, not by consuming internal APIs (REV2 §7).",
        helpLinkUri: "https://github.com/BechsteinDigital/callora/blob/main/docs/adr/ADR-012-Ein-Core-Extensibility.md");

    /// <summary>The stable diagnostic id for deriving from or implementing an internal type.</summary>
    public const string InheritanceDiagnosticId = "CAL0002";

    internal static readonly DiagnosticDescriptor InheritanceRule = new(
        id: InheritanceDiagnosticId,
        title: "Deriving from or implementing a [CalloraInternal] type",
        messageFormat: "'{0}' is marked [CalloraInternal] and is not an extension point; plugins must not derive from or implement it{1}",
        category: "Callora.Extensibility",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types marked [CalloraInternal] are not sanctioned extension points. Plugins extend Callora only through types marked [CalloraExtensible] or other documented mechanisms, not by deriving from or implementing internal types (REV2 §7).",
        helpLinkUri: "https://github.com/BechsteinDigital/callora/blob/main/docs/adr/ADR-012-Ein-Core-Extensibility.md");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule, InheritanceRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        // Framework assemblies (Core/Administration/Workspace/CLI) legitimately consume
        // their own internal surface; only plugin/consumer compilations are gated.
        if (IsFrameworkAssembly(context.Options.AnalyzerConfigOptionsProvider.GlobalOptions))
        {
            return;
        }

        var marker = context.Compilation.GetTypeByMetadataName(InternalAttributeMetadataName);
        if (marker is null)
        {
            // The marker assembly is not referenced → nothing to enforce.
            return;
        }

        var enforcement = new CalloraInternalConsumptionEnforcement(marker, context.Compilation.Assembly);

        context.RegisterOperationAction(
            enforcement.AnalyzeOperation,
            OperationKind.Invocation,
            OperationKind.ObjectCreation,
            OperationKind.PropertyReference,
            OperationKind.FieldReference,
            OperationKind.EventReference,
            OperationKind.MethodReference,
            OperationKind.TypeOf);

        context.RegisterSymbolAction(
            enforcement.AnalyzeSymbol,
            SymbolKind.Method,
            SymbolKind.Property,
            SymbolKind.Field,
            SymbolKind.Event);

        // CAL0002: deriving from or implementing an internal type via the base list —
        // the inheritance vector the member/usage actions above do not cover.
        context.RegisterSymbolAction(enforcement.AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static bool IsFrameworkAssembly(AnalyzerConfigOptions options)
        => options.TryGetValue(FrameworkAssemblyProperty, out var value)
           && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

}
