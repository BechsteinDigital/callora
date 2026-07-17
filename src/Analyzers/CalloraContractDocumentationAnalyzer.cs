using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Callora.Analyzers;

/// <summary>
/// CAL0003 — reports a public type or member on the Callora plugin contract surface
/// that has no XML documentation. The contract surface is what a plugin author reads
/// to write a plugin, so it must stay documented; the internal public surface (which
/// is public for technical reasons only, REV2 §7.1) is deliberately out of scope.
/// </summary>
/// <remarks>
/// The contract surface is identified structurally, not by visibility: a symbol is on
/// it when its namespace ends in <c>.Contracts</c>, or is (nested in) an
/// <c>Extensibility</c> namespace, or the symbol or an enclosing type carries
/// <c>[CalloraExtensible]</c>. This mirrors how the surface was curated for R5c and
/// gives the .NET equivalent of enforcing docs on a hand-picked API package — something
/// the built-in CS1591 cannot do, since a compiler warning cannot be escalated from
/// <c>none</c> to <c>error</c> for a scattered subset of files.
/// <para>
/// Known gap: a few consumption contracts live outside a <c>.Contracts</c> namespace
/// (e.g. <c>ICalloraPluginCatalog</c>, <c>ICalloraPluginRuntime</c> in
/// <c>…Application.Plugins</c>, <c>IHostApplicationEventSubscriber&lt;T&gt;</c> in
/// <c>…Application.Events</c>). They are documented but not enforced here; the durable
/// fix is to move them into a <c>.Contracts</c> namespace rather than tag them
/// <c>[CalloraExtensible]</c>, whose meaning is "plugins may implement" and does not fit
/// a consumption contract.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CalloraContractDocumentationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The stable diagnostic id for missing contract-surface documentation.</summary>
    public const string DiagnosticId = "CAL0003";

    private const string ExtensibleAttributeMetadataName = "Callora.Core.Extensibility.CalloraExtensibleAttribute";

    internal static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Missing XML documentation on the plugin contract surface",
        messageFormat: "'{0}' is on the Callora plugin contract surface and must have XML documentation",
        category: "Callora.Extensibility",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Public types and members that plugin authors implement or consume (namespaces ending in .Contracts, Extensibility types, and [CalloraExtensible] members) form the documented plugin contract. They must carry XML documentation so the contract stays legible; the internal public surface is out of scope (REV2 §7).",
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
        // The marker is optional: the namespace rules stand on their own, but resolving
        // the attribute once here keeps the per-symbol check allocation-free.
        var extensibleMarker = context.Compilation.GetTypeByMetadataName(ExtensibleAttributeMetadataName);
        var enforcement = new CalloraContractDocumentationEnforcement(extensibleMarker);

        context.RegisterSymbolAction(
            enforcement.AnalyzeSymbol,
            SymbolKind.NamedType,
            SymbolKind.Method,
            SymbolKind.Property,
            SymbolKind.Field,
            SymbolKind.Event);
    }
}
