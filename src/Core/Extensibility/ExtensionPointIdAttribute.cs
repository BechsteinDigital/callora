namespace Callora.Core.Extensibility;

/// <summary>
/// Marks a string parameter that carries a Callora extension-point id. CAL0004 checks
/// that arguments passed to such a parameter reference a <c>CalloraExtensionPoints</c>
/// constant, so a mistyped or unknown id surfaces as a compile error with IDE completion
/// rather than as a runtime activation failure in the extension synchronizer.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
public sealed class ExtensionPointIdAttribute : Attribute;
