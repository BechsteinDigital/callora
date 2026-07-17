using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Linq;
using System.Threading;

namespace Callora.Analyzers;

/// <summary>
/// Per-compilation state for <see cref="CalloraContractDocumentationAnalyzer"/>. Holding
/// the resolved marker here keeps the analyzer instance stateless and concurrency-safe.
/// </summary>
internal sealed class CalloraContractDocumentationEnforcement
{
    private const string ContractNamespaceSuffix = ".Contracts";
    private const string ExtensibilitySegment = "Extensibility";

    private readonly INamedTypeSymbol? _extensibleMarker;

    public CalloraContractDocumentationEnforcement(INamedTypeSymbol? extensibleMarker)
    {
        _extensibleMarker = extensibleMarker;
    }

    public void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        var symbol = context.Symbol;

        // Only what a plugin author actually declares in source, and only the externally
        // reachable surface. Compiler-generated members (record ceremony, enum backing
        // field) and property/event accessors are covered by their owning declaration.
        if (symbol.IsImplicitlyDeclared ||
            !IsEffectivelyPublic(symbol) ||
            IsAccessor(symbol))
        {
            return;
        }

        var location = symbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (location is null || !IsOnContractSurface(symbol))
        {
            return;
        }

        if (HasDocumentation(symbol, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            CalloraContractDocumentationAnalyzer.Rule,
            location,
            symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    private static bool IsAccessor(ISymbol symbol)
        => symbol is IMethodSymbol { AssociatedSymbol: not null };

    /// <summary>
    /// True only when the symbol is reachable from outside the assembly: its own
    /// declared accessibility is public and every enclosing type is public too. Interface
    /// members declare as public even inside an internal interface, so the containing-type
    /// walk is what keeps a non-exported contract off the surface.
    /// </summary>
    private static bool IsEffectivelyPublic(ISymbol symbol)
    {
        if (symbol.DeclaredAccessibility != Accessibility.Public)
        {
            return false;
        }

        for (var container = symbol.ContainingType; container is not null; container = container.ContainingType)
        {
            if (container.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// A symbol is on the contract surface when it (or an enclosing type) carries
    /// <c>[CalloraExtensible]</c>, or its namespace ends in <c>.Contracts</c>, or it
    /// lives under an <c>Extensibility</c> namespace.
    /// </summary>
    private bool IsOnContractSurface(ISymbol symbol)
    {
        for (ISymbol? current = symbol; current is not null; current = current.ContainingType)
        {
            if (HasExtensibleMarker(current))
            {
                return true;
            }
        }

        for (var ns = symbol.ContainingNamespace; ns is { IsGlobalNamespace: false }; ns = ns.ContainingNamespace)
        {
            if (ns.Name == ExtensibilitySegment)
            {
                return true;
            }
        }

        var fullNamespace = symbol.ContainingNamespace?.ToDisplayString();
        return fullNamespace is not null
            && fullNamespace.EndsWith(ContractNamespaceSuffix, StringComparison.Ordinal);
    }

    private bool HasExtensibleMarker(ISymbol symbol)
        => _extensibleMarker is not null
           && symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, _extensibleMarker));

    private static bool HasDocumentation(ISymbol symbol, CancellationToken cancellationToken)
    {
        var xml = symbol.GetDocumentationCommentXml(
            preferredCulture: null,
            expandIncludes: false,
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(xml))
        {
            return false;
        }

        // A present <summary>, <param> (positional records) or <inheritdoc/> all count
        // as a documented contract.
        return xml!.IndexOf("<summary", StringComparison.Ordinal) >= 0
            || xml.IndexOf("<param", StringComparison.Ordinal) >= 0
            || xml.IndexOf("<inheritdoc", StringComparison.Ordinal) >= 0;
    }
}
