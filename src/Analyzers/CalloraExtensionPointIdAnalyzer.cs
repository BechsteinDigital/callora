using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;

namespace Callora.Analyzers;

/// <summary>
/// CAL0004 — reports a Callora extension-point id passed as a raw string instead of a
/// <c>CalloraExtensionPoints</c> constant. It fires on any argument to a parameter marked
/// <c>[ExtensionPointId]</c> whose value is a string literal: a mistyped id ("not a known
/// extension point") or a hard-coded known id ("use the constant") both surface at compile
/// time with IDE completion, rather than as a runtime activation failure.
/// </summary>
/// <remarks>
/// A dynamic (non-constant) value is allowed — the analyzer only judges what it can see at
/// compile time. A reference to a <c>CalloraExtensionPoints</c> constant is the sanctioned
/// form and never reported.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CalloraExtensionPointIdAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The stable diagnostic id for a raw extension-point id.</summary>
    public const string DiagnosticId = "CAL0004";

    private const string MarkerMetadataName = "Callora.Core.Extensibility.ExtensionPointIdAttribute";
    private const string ConstantsMetadataName = "Callora.Core.Domain.Extensions.CalloraExtensionPoints";

    internal static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Extension-point id must reference a CalloraExtensionPoints constant",
        messageFormat: "{0}",
        category: "Callora.Extensibility",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Extension-point ids are identified by the [ExtensionPointId] parameter marker and must come from CalloraExtensionPoints constants, so a mistyped or unknown id is a compile error with IDE completion rather than a runtime activation failure (REV2 §8.2).",
        helpLinkUri: "https://github.com/BechsteinDigital/callora/blob/main/docs/adr/ADR-012-Ein-Core-Extensibility.md");

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
        var marker = context.Compilation.GetTypeByMetadataName(MarkerMetadataName);
        if (marker is null)
        {
            // The marker assembly is not referenced → nothing to enforce.
            return;
        }

        var constantsType = context.Compilation.GetTypeByMetadataName(ConstantsMetadataName);
        var knownIds = CollectKnownIds(constantsType);

        var enforcement = new CalloraExtensionPointIdEnforcement(marker, constantsType, knownIds);
        context.RegisterOperationAction(enforcement.AnalyzeArgument, OperationKind.Argument);
    }

    private static ImmutableHashSet<string> CollectKnownIds(INamedTypeSymbol? constantsType)
    {
        if (constantsType is null)
        {
            return ImmutableHashSet<string>.Empty;
        }

        var builder = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);
        foreach (var member in constantsType.GetMembers())
        {
            if (member is IFieldSymbol { IsConst: true, ConstantValue: string value })
            {
                builder.Add(value);
            }
        }

        return builder.ToImmutable();
    }
}
