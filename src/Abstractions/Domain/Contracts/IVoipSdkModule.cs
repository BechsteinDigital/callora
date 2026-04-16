namespace Callora.Modules.Abstractions.Domain.Contracts;

/// <summary>
/// Base contract for all Callora modules.
/// </summary>
public interface ICalloraModule
{
    /// <summary>Stable module identifier.</summary>
    string ModuleId { get; }

    /// <summary>Module display name.</summary>
    string DisplayName { get; }
}
