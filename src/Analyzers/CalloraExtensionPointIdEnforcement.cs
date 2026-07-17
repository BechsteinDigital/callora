using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Linq;

namespace Callora.Analyzers;

/// <summary>
/// Per-compilation state for <see cref="CalloraExtensionPointIdAnalyzer"/>. Holding the
/// resolved marker, constants type and known ids here keeps the analyzer instance stateless
/// and concurrency-safe.
/// </summary>
internal sealed class CalloraExtensionPointIdEnforcement
{
    private readonly INamedTypeSymbol _marker;
    private readonly INamedTypeSymbol? _constantsType;
    private readonly ImmutableHashSet<string> _knownIds;

    public CalloraExtensionPointIdEnforcement(
        INamedTypeSymbol marker,
        INamedTypeSymbol? constantsType,
        ImmutableHashSet<string> knownIds)
    {
        _marker = marker;
        _constantsType = constantsType;
        _knownIds = knownIds;
    }

    public void AnalyzeArgument(OperationAnalysisContext context)
    {
        var argument = (IArgumentOperation)context.Operation;
        if (argument.Parameter is null || !HasMarker(argument.Parameter))
        {
            return;
        }

        var value = argument.Value;

        // A reference to a CalloraExtensionPoints constant is the sanctioned form.
        if (value is IFieldReferenceOperation fieldReference &&
            _constantsType is not null &&
            SymbolEqualityComparer.Default.Equals(fieldReference.Field.ContainingType, _constantsType))
        {
            return;
        }

        // Only a compile-time-constant string is judged; a dynamic value is allowed.
        if (value.ConstantValue is not { HasValue: true, Value: string id })
        {
            return;
        }

        var message = _knownIds.Contains(id)
            ? $"Use a CalloraExtensionPoints constant instead of the raw extension-point id \"{id}\""
            : $"\"{id}\" is not a known Callora extension-point id; use a CalloraExtensionPoints constant";

        context.ReportDiagnostic(Diagnostic.Create(
            CalloraExtensionPointIdAnalyzer.Rule,
            value.Syntax.GetLocation(),
            message));
    }

    private bool HasMarker(IParameterSymbol parameter)
        => parameter.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, _marker));
}
