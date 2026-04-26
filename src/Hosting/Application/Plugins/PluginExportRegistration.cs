namespace Callora.Hosting.Application.Plugins;

internal sealed record PluginExportRegistration(
    string PluginId,
    Type ContractType,
    object Service,
    long Sequence);
