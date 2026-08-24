using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Linq;

namespace Callora.Analyzers;

/// <summary>
/// Per-compilation enforcement state for CAL0005. Held here rather than in analyzer fields
/// so the analyzer instance stays stateless and safe for concurrent execution.
/// </summary>
internal sealed class CalloraDeprecatedConsumptionEnforcement
{
    private readonly INamedTypeSymbol _marker;
    private readonly IAssemblySymbol _consumer;

    public CalloraDeprecatedConsumptionEnforcement(INamedTypeSymbol marker, IAssemblySymbol consumer)
    {
        _marker = marker;
        _consumer = consumer;
    }

    public void AnalyzeOperation(OperationAnalysisContext context)
    {
        var culprits = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        switch (context.Operation)
        {
            case IInvocationOperation op:
                Collect(op.TargetMethod, culprits);
                break;
            case IObjectCreationOperation op:
                Collect(op.Constructor, culprits);
                CollectType(op.Type, culprits);
                break;
            case IPropertyReferenceOperation op:
                Collect(op.Property, culprits);
                break;
            case IFieldReferenceOperation op:
                Collect(op.Field, culprits);
                break;
            case IEventReferenceOperation op:
                Collect(op.Event, culprits);
                break;
            case IMethodReferenceOperation op:
                Collect(op.Method, culprits);
                break;
        }

        foreach (var culprit in culprits)
        {
            context.ReportDiagnostic(Describe(culprit, context.Operation.Syntax.GetLocation()));
        }
    }

    public void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var culprits = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        CollectType(type.BaseType, culprits);
        foreach (var contract in type.Interfaces)
        {
            CollectType(contract, culprits);
        }

        if (culprits.Count == 0)
        {
            return;
        }

        var location = type.Locations.FirstOrDefault() ?? Location.None;
        foreach (var culprit in culprits)
        {
            context.ReportDiagnostic(Describe(culprit, location));
        }
    }

    private void Collect(ISymbol? symbol, HashSet<ISymbol> culprits)
    {
        if (symbol is null || SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, _consumer))
        {
            return;
        }

        if (Announcement(symbol) is not null)
        {
            culprits.Add(symbol);
            return;
        }

        // A member of a deprecated type inherits the type's announcement, matching how the
        // surface file records it — otherwise retiring a type would only be visible to
        // authors who happen to touch a separately marked member.
        if (symbol.ContainingType is { } containing && Announcement(containing) is not null)
        {
            culprits.Add(containing);
        }
    }

    private void CollectType(ITypeSymbol? type, HashSet<ISymbol> culprits)
    {
        if (type is null || SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _consumer))
        {
            return;
        }

        if (Announcement(type) is not null)
        {
            culprits.Add(type);
        }
    }

    private AttributeData? Announcement(ISymbol symbol) =>
        symbol.GetAttributes().FirstOrDefault(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, _marker));

    private Diagnostic Describe(ISymbol culprit, Location location)
    {
        var announcement = Announcement(culprit);
        var since = ArgumentAt(announcement, 0) ?? "an earlier version";
        var errorsIn = ArgumentAt(announcement, 1) ?? "a future contract version";
        var replacement = NamedArgument(announcement, "Replacement");

        return Diagnostic.Create(
            CalloraDeprecatedConsumptionAnalyzer.Rule,
            location,
            culprit.ToDisplayString(),
            since,
            errorsIn,
            replacement is null ? string.Empty : $"; use {replacement} instead");
    }

    private static string? ArgumentAt(AttributeData? attribute, int index) =>
        attribute is not null && attribute.ConstructorArguments.Length > index
            ? attribute.ConstructorArguments[index].Value as string
            : null;

    private static string? NamedArgument(AttributeData? attribute, string name) =>
        attribute?.NamedArguments
            .FirstOrDefault(pair => pair.Key == name)
            .Value.Value as string;
}
