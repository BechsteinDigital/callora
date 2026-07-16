using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Callora.Analyzers;

/// <summary>
/// Per-compilation enforcement state. Holding the marker and consumer assembly here
/// (rather than in analyzer fields) keeps the analyzer instance stateless and safe
/// for concurrent execution.
/// </summary>
internal sealed class CalloraInternalConsumptionEnforcement
{
    private readonly INamedTypeSymbol _marker;
    private readonly IAssemblySymbol _consumer;

    public CalloraInternalConsumptionEnforcement(INamedTypeSymbol marker, IAssemblySymbol consumer)
    {
        _marker = marker;
        _consumer = consumer;
    }

    public void AnalyzeOperation(OperationAnalysisContext context)
    {
        // Collect the marked culprits reachable from this operation, deduped per
        // operation so one expression yields at most one diagnostic per culprit.
        // Generic type arguments are unwrapped here too (e.g. new List<Marked>(),
        // typeof(List<Marked>), Factory.Create<Marked>()), matching the signature
        // path so operation and declaration coverage stay consistent.
        var culprits = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        switch (context.Operation)
        {
            case IInvocationOperation op:
                CollectSymbol(op.TargetMethod, culprits);
                CollectTypeArguments(op.TargetMethod.TypeArguments, culprits);
                break;
            case IObjectCreationOperation op:
                CollectSymbol(op.Constructor, culprits);
                CollectType(op.Type, culprits);
                break;
            case IPropertyReferenceOperation op:
                CollectSymbol(op.Property, culprits);
                break;
            case IFieldReferenceOperation op:
                CollectSymbol(op.Field, culprits);
                break;
            case IEventReferenceOperation op:
                CollectSymbol(op.Event, culprits);
                break;
            case IMethodReferenceOperation op:
                CollectSymbol(op.Method, culprits);
                CollectTypeArguments(op.Method.TypeArguments, culprits);
                break;
            case ITypeOfOperation op:
                CollectType(op.TypeOperand, culprits);
                break;
        }

        if (culprits.Count == 0)
        {
            return;
        }

        var location = context.Operation.Syntax.GetLocation();
        foreach (var culprit in culprits)
        {
            Emit(culprit, location, CalloraInternalConsumptionAnalyzer.Rule, context.ReportDiagnostic);
        }
    }

    private void CollectSymbol(ISymbol? symbol, HashSet<ISymbol> culprits)
    {
        if (symbol is null)
        {
            return;
        }

        var marked = FindMarked(symbol);
        if (marked is not null)
        {
            culprits.Add(marked);
        }
    }

    private void CollectType(ITypeSymbol? type, HashSet<ISymbol> culprits)
    {
        var marked = FindMarkedInType(type);
        if (marked is not null)
        {
            culprits.Add(marked);
        }
    }

    private void CollectTypeArguments(ImmutableArray<ITypeSymbol> typeArguments, HashSet<ISymbol> culprits)
    {
        foreach (var typeArgument in typeArguments)
        {
            CollectType(typeArgument, culprits);
        }
    }

    public void AnalyzeSymbol(SymbolAnalysisContext context)
    {
        var location = context.Symbol.Locations.FirstOrDefault(l => l.IsInSource);
        if (location is null)
        {
            return;
        }

        switch (context.Symbol)
        {
            case IMethodSymbol method:
                // Accessors/backing members are covered by their associated property/event.
                if (method.AssociatedSymbol is not null)
                {
                    return;
                }

                ReportType(method.ReturnType, location, context.ReportDiagnostic);
                foreach (var parameter in method.Parameters)
                {
                    ReportType(parameter.Type, location, context.ReportDiagnostic);
                }

                break;
            case IPropertySymbol property:
                ReportType(property.Type, location, context.ReportDiagnostic);
                break;
            case IFieldSymbol field:
                ReportType(field.Type, location, context.ReportDiagnostic);
                break;
            case IEventSymbol @event:
                ReportType(@event.Type, location, context.ReportDiagnostic);
                break;
        }
    }

    public void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var location = type.Locations.FirstOrDefault(l => l.IsInSource);
        if (location is null)
        {
            return;
        }

        // Only the directly declared base list — what the plugin author actually wrote —
        // is a violation; transitively inherited internal interfaces are not the author's.
        var culprits = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        if (type.BaseType is not null && HasMarker(type.BaseType))
        {
            culprits.Add(type.BaseType);
        }

        foreach (var iface in type.Interfaces)
        {
            if (HasMarker(iface))
            {
                culprits.Add(iface);
            }
        }

        foreach (var culprit in culprits)
        {
            Emit(culprit, location, CalloraInternalConsumptionAnalyzer.InheritanceRule, context.ReportDiagnostic);
        }
    }

    private void ReportType(ITypeSymbol? type, Location location, Action<Diagnostic> report)
    {
        var marked = FindMarkedInType(type);
        if (marked is not null)
        {
            Emit(marked, location, CalloraInternalConsumptionAnalyzer.Rule, report);
        }
    }

    private void Emit(ISymbol marked, Location location, DiagnosticDescriptor rule, Action<Diagnostic> report)
    {
        // Only cross-assembly use of the framework's marked API is a violation;
        // a plugin using its own [CalloraInternal] type is its own business.
        if (SymbolEqualityComparer.Default.Equals(marked.ContainingAssembly, _consumer))
        {
            return;
        }

        var reason = GetReason(marked);
        var suffix = string.IsNullOrEmpty(reason) ? "." : ": " + reason;
        report(Diagnostic.Create(
            rule,
            location,
            marked.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            suffix));
    }

    /// <summary>Returns the symbol or nearest enclosing type carrying the marker, or null.</summary>
    private ISymbol? FindMarked(ISymbol symbol)
    {
        for (ISymbol? current = symbol; current is not null; current = current.ContainingType)
        {
            if (HasMarker(current))
            {
                return current;
            }
        }

        return null;
    }

    /// <summary>Unwraps arrays and generic arguments to find a marked type in a type reference.</summary>
    private ITypeSymbol? FindMarkedInType(ITypeSymbol? type)
    {
        switch (type)
        {
            case null:
                return null;
            case IArrayTypeSymbol array:
                return FindMarkedInType(array.ElementType);
            case INamedTypeSymbol named:
                var self = FindMarked(named) as ITypeSymbol;
                if (self is not null)
                {
                    return self;
                }

                foreach (var argument in named.TypeArguments)
                {
                    var nested = FindMarkedInType(argument);
                    if (nested is not null)
                    {
                        return nested;
                    }
                }

                return null;
            default:
                return FindMarked(type) as ITypeSymbol;
        }
    }

    private bool HasMarker(ISymbol symbol)
        => symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, _marker));

    private string? GetReason(ISymbol symbol)
    {
        var attribute = symbol.GetAttributes()
            .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, _marker));

        if (attribute is null)
        {
            return null;
        }

        if (attribute.ConstructorArguments.Length == 1
            && attribute.ConstructorArguments[0].Value is string ctorReason)
        {
            return ctorReason;
        }

        var named = attribute.NamedArguments.FirstOrDefault(kv => kv.Key == "Reason");
        return named.Value.Value as string;
    }
}
