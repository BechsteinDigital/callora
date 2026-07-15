namespace Callora.Core.Application.Plugins;

public sealed record PluginContractVersionSupport(
    string ContractVersion,
    PluginContractSupportStatus Status,
    string Message);
