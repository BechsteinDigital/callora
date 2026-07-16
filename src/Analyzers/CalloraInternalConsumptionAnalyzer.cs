using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

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

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Consuming a [CalloraInternal] API from outside the framework",
        messageFormat: "'{0}' is marked [CalloraInternal] and is not part of the Callora plugin contract; it must not be consumed outside Callora framework assemblies{1}",
        category: "Callora.Extensibility",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Types and members marked [CalloraInternal] are visible for technical reasons only and are not a stable plugin contract. Plugins must extend Callora through documented extension points, not by consuming internal APIs (REV2 §7).",
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

        var enforcement = new Enforcement(marker, context.Compilation.Assembly);

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
    }

    private static bool IsFrameworkAssembly(AnalyzerConfigOptions options)
        => options.TryGetValue(FrameworkAssemblyProperty, out var value)
           && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Per-compilation enforcement state. Holding the marker and consumer assembly here
    /// (rather than in analyzer fields) keeps the analyzer instance stateless and safe
    /// for concurrent execution.
    /// </summary>
    private sealed class Enforcement
    {
        private readonly INamedTypeSymbol _marker;
        private readonly IAssemblySymbol _consumer;

        public Enforcement(INamedTypeSymbol marker, IAssemblySymbol consumer)
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
                Emit(culprit, location, context.ReportDiagnostic);
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

        private void ReportType(ITypeSymbol? type, Location location, Action<Diagnostic> report)
        {
            var marked = FindMarkedInType(type);
            if (marked is not null)
            {
                Emit(marked, location, report);
            }
        }

        private void Emit(ISymbol marked, Location location, Action<Diagnostic> report)
        {
            // Only cross-assembly consumption of the framework's marked API is a violation;
            // a plugin using its own [CalloraInternal] type is its own business.
            if (SymbolEqualityComparer.Default.Equals(marked.ContainingAssembly, _consumer))
            {
                return;
            }

            var reason = GetReason(marked);
            var suffix = string.IsNullOrEmpty(reason) ? "." : ": " + reason;
            report(Diagnostic.Create(
                Rule,
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
}
