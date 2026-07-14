namespace Callora.Host.Backend.Application.Plugins;

public sealed record LocalPluginInstallSourceResolveResult(
    bool IsSuccess,
    string PluginId,
    string? AssemblyPath,
    string? EntryTypeName,
    bool UsedBuild,
    string Message,
    string? ErrorCode = null);
