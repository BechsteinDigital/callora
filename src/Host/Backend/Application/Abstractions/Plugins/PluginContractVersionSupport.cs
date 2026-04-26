namespace Callora.Host.Backend.Application.Abstractions.Plugins;

public sealed record PluginContractVersionSupport(
    string ContractVersion,
    PluginContractSupportStatus Status,
    string Message);
