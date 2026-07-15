namespace Callora.Administration.Api;

public sealed record PluginContractSupportApiResponse(
    string ContractVersion,
    string SupportStatus,
    bool IsInstallable,
    bool EmitsWarning,
    string Message);
