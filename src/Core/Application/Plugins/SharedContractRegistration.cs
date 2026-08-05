namespace Callora.Core.Application.Plugins;

/// <summary>
/// One contract assembly the host shares across plugin load contexts (#125 block D).
/// </summary>
/// <param name="AssemblyName">Simple assembly name plugins bind against.</param>
/// <param name="Version">Version currently pinned for the host's lifetime.</param>
/// <param name="DeclaringPluginId">Plugin whose manifest declared it, when known.</param>
/// <param name="IsHostProvided">
/// True when the host application itself references the assembly, so it already lives in the
/// default load context and the registry only records it. False when a plugin brought it and the
/// registry loaded it.
/// </param>
public sealed record SharedContractRegistration(
    string AssemblyName,
    string Version,
    string? DeclaringPluginId,
    bool IsHostProvided);
