namespace Callora.Host.Backend.Application.Plugins;

public static class PluginContractVersionPolicy
{
    private static readonly PluginContractVersionSupport[] Policies =
    [
        new(
            ContractVersion: "v2",
            Status: PluginContractSupportStatus.Supported,
            Message: "Actively supported contract version."),
        new(
            ContractVersion: "v1",
            Status: PluginContractSupportStatus.Deprecated,
            Message: "Deprecated contract version. Installation is allowed with warning."),
        new(
            ContractVersion: "v0",
            Status: PluginContractSupportStatus.Removed,
            Message: "Removed contract version. Installation is blocked.")
    ];

    private static readonly Dictionary<string, PluginContractVersionSupport> PoliciesByVersion =
        Policies.ToDictionary(
            x => x.ContractVersion,
            StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<PluginContractVersionSupport> GetAll() => Policies;

    public static bool TryGet(string contractVersion, out PluginContractVersionSupport support) =>
        PoliciesByVersion.TryGetValue(contractVersion, out support!);
}
