namespace Callora.Administration.Api;

/// <summary>
/// One contract an installation offers, with everything an operator needs before touching it
/// (#125 block D).
/// </summary>
/// <param name="AssemblyName">Simple assembly name plugins bind against.</param>
/// <param name="Version">Version currently pinned for the host's lifetime.</param>
/// <param name="DeclaringPluginId">Plugin that brought it, or null when the host provides it.</param>
/// <param name="IsHostProvided">Whether the host application itself provides the assembly.</param>
/// <param name="RequiresRestartToChange">Whether replacing it needs a host restart. Today always true.</param>
/// <param name="Dependents">Installed plugins bound to it, and whether their range is satisfied.</param>
public sealed record ContractCatalogApiResponse(
    string AssemblyName,
    string Version,
    string? DeclaringPluginId,
    bool IsHostProvided,
    bool RequiresRestartToChange,
    IReadOnlyList<ContractDependentApiResponse> Dependents);
