namespace Callora.Core.Api;

public sealed record UpdateNuGetPluginRequest(
    string PackageId,
    string PackageVersion,
    string? AssemblyFileName = null,
    string? EntryTypeName = null,
    string? RequestedBy = null);
