namespace Callora.Modules.Abstractions.Domain.Entitlements;

/// <summary>
/// Minimal gate abstraction used by the SDK to decide whether a module may be used.
/// </summary>
public interface IModuleAccessGate
{
    /// <summary>
    /// Returns <c>true</c> when the module is enabled for the current license context.
    /// </summary>
    bool IsModuleEnabled(string moduleId);
}
