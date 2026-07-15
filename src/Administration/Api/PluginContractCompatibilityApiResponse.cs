namespace Callora.Administration.Api;

public sealed record PluginContractCompatibilityApiResponse(
    string HostVersion,
    string CoreVersion,
    string ContractVersion,
    string Result,
    bool IsCompatible,
    string Message);
