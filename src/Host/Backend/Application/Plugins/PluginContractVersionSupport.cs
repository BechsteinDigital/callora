namespace Callora.Host.Backend.Application.Plugins;

public sealed record PluginContractVersionSupport(
    string ContractVersion,
    PluginContractSupportStatus Status,
    string Message);
