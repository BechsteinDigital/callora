namespace Callora.Administration.Api;

public sealed record InstallNuGetPluginRequest(
    string PackageId,
    string PackageVersion,
    string? AssemblyFileName = null,
    string? EntryTypeName = null,
    string? RequestedBy = null);
