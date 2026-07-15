namespace Callora.Core.Application.Lifecycle;

public sealed record InstallNuGetPluginCommand(
    string PackageId,
    string PackageVersion,
    string? AssemblyFileName = null,
    string? EntryTypeName = null,
    string? RequestedBy = null);
