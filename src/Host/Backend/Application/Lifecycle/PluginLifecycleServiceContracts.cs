namespace Callora.Host.Backend.Application.Lifecycle;

public enum PluginLifecycleServiceStatus
{
    Ok = 0,
    BadRequest = 1,
    Forbidden = 2,
}

public sealed record PluginLifecycleServiceResult(
    PluginLifecycleServiceStatus Status,
    bool IsSuccess,
    string? PluginId = null,
    string? Message = null);

public sealed record PluginInstallationSnapshot(
    string PluginId,
    string DisplayName,
    string AssemblyPath,
    string? EntryTypeName,
    int State,
    DateTimeOffset InstalledAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record InstallPluginCommand(
    string AssemblyPath,
    string? EntryTypeName = null,
    string? RequestedBy = null);

public sealed record InstallNuGetPluginCommand(
    string PackageId,
    string PackageVersion,
    string? AssemblyFileName = null,
    string? EntryTypeName = null,
    string? RequestedBy = null);

public sealed record PluginLifecycleCommand(
    string PluginId,
    string? RequestedBy = null);
