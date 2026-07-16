using System;
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
            ISymbol? referenced = context.Operation switch
            {
                IInvocationOperation op => op.TargetMethod,
                IObjectCreationOperation op => (ISymbol?)op.Constructor ?? op.Type,
                IPropertyReferenceOperation op => op.Property,
                IFieldReferenceOperation op => op.Field,
                IEventReferenceOperation op => op.Event,
                IMethodReferenceOperation op => op.Method,
                ITypeOfOperation op => op.TypeOperand,
                _ => null,
            };

            if (referenced is null)
            {
                return;
            }

            Report(referenced, context.Operation.Syntax.GetLocation(), context.ReportDiagnostic);
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
                Report(marked, location, report);
            }
        }

        private void Report(ISymbol referenced, Location location, Action<Diagnostic> report)
        {
            var marked = FindMarked(referenced);
            if (marked is null)
            {
                return;
            }

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
