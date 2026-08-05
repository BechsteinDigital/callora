namespace Callora.Core.Application.Plugins;

/// <summary>
/// One contract as the catalog reports it (#125 block D): what is shared, at which version, who
/// brought it, and who would notice if it changed.
/// <para>
/// Without this, plugin combinability is insider knowledge. An operator installing a partner
/// plugin has no way to see which contracts an installation offers, and no way to find out what a
/// contract update would break before applying it.
/// </para>
/// </summary>
/// <param name="AssemblyName">Simple assembly name plugins bind against.</param>
/// <param name="Version">Version currently pinned for the host's lifetime.</param>
/// <param name="DeclaringPluginId">Plugin whose manifest declared it, or null for a host-provided contract.</param>
/// <param name="IsHostProvided">Whether the host application itself provides the assembly.</param>
/// <param name="RequiresRestartToChange">
/// Always true today, and stated rather than implied: a shared contract is loaded once into the
/// default context and stays pinned for the host's lifetime, so replacing it needs a restart while
/// everything else about the plugin stays hot-swappable.
/// </param>
/// <param name="Dependents">Installed plugins declaring a dependency on this contract.</param>
public sealed record ContractCatalogEntry(
    string AssemblyName,
    string Version,
    string? DeclaringPluginId,
    bool IsHostProvided,
    bool RequiresRestartToChange,
    IReadOnlyList<ContractDependent> Dependents);
